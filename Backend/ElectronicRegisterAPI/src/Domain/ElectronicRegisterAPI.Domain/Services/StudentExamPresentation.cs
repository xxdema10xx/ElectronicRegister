using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Persistence;
using System.Security.Cryptography;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Domain.Services;

public static class StudentExamPresentation
{
    public static string CreateOrder(Exam exam, Func<int, int>? next = null)
    {
        next ??= RandomNumberGenerator.GetInt32;
        T[] Shuffle<T>(IEnumerable<T> source)
        {
            var values = source.ToArray();
            for (var index = values.Length - 1; index > 0; index--)
            {
                var target = next(index + 1);
                if (target < 0 || target > index)
                    throw new InvalidOperationException("Invalid random index.");
                (values[index], values[target]) = (values[target], values[index]);
            }

            return values;
        }

        return JsonSerializer.Serialize(new Order(Shuffle(exam.Questions.OrderBy(q => q.Position).ThenBy(q => q.Id).Select(q => q.Id)), exam.Questions.Where(q => q.Type != QuestionType.Open).ToDictionary(q => q.Id, q => Shuffle(q.Options.OrderBy(o => o.Position).ThenBy(o => o.Id).Select(o => o.Id)))));
    }

    public static ExamForStudentDto Build(Exam exam, string? json)
    {
        var order = json is null ? null : JsonSerializer.Deserialize<Order>(json) ?? throw new InvalidOperationException("Invalid persisted display order.");
        var questions = exam.Questions.OrderBy(q => q.Position).ThenBy(q => q.Id).ToArray();
        if (order is not null)
        {
            if (order.Questions.Length != questions.Length || order.Questions.Distinct().Count() != questions.Length || order.Questions.Except(questions.Select(q => q.Id)).Any())
                throw new InvalidOperationException("Display order does not match exam.");
            var byId = questions.ToDictionary(q => q.Id);
            questions = order.Questions.Select(id => byId[id]).ToArray();
        }

        return new ExamForStudentDto
        {
            Id = exam.Id,
            Name = exam.Name,
            Description = exam.Description,
            AvailableUntil = exam.AvailableUntil,
            DurationMinutes = exam.DurationMinutes,
            TotalPoints = exam.TotalPoints,
            Questions = questions.Select((q, index) =>
            {
                var options = q.Options.OrderBy(o => o.Position).ThenBy(o => o.Id).ToArray();
                if (order is not null && q.Type != QuestionType.Open)
                {
                    if (!order.Options.TryGetValue(q.Id, out var ids) || ids.Length != options.Length || ids.Distinct().Count() != options.Length || ids.Except(options.Select(o => o.Id)).Any())
                        throw new InvalidOperationException("Display order does not match options.");
                    var byId = options.ToDictionary(o => o.Id);
                    options = ids.Select(id => byId[id]).ToArray();
                }

                return new QuestionForStudentDto
                {
                    Id = q.Id,
                    Position = order is null ? q.Position : index + 1,
                    Text = q.Text,
                    Type = q.Type,
                    Points = q.Points,
                    Required = q.Required,
                    Options = options.Select((o, position) => new QuestionOptionForStudentDto { Id = o.Id, Position = order is null ? o.Position : position + 1, Text = o.Text }).ToList()
                };
            }).ToList()
        };
    }
}
