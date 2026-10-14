
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Business.Services;

public class ExamService : IExamService
{
    public void EnsureValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Il nome dell'esame è obbligatorio.");

        if (name.Length > 150)
            throw new ArgumentException("Il nome dell'esame non può superare i 150 caratteri.");
    }

    public void EnsureValidSchedule(DateTime availableFrom, DateTime availableUntil, int durationMinutes)
    {
        if (availableFrom >= availableUntil)
            throw new ArgumentException("La data di inizio deve precedere la data di fine.");

        if (durationMinutes <= 5)
            throw new ArgumentException("La durata dell'esame deve essere maggiore di 5 minuti.");

        var availableWindow = availableUntil - availableFrom;

        if (durationMinutes > availableWindow.TotalMinutes)
            throw new ArgumentException("La durata dell'esame non può superare la finestra di disponibilità.");
    }

    public void EnsureValidPassingPercent(decimal passingPercent)
    {
        if (passingPercent <= 0 || passingPercent > 100)
            throw new ArgumentException("La percentuale di sufficienza deve essere maggiore di 0 e minore o uguale a 100.");
    }

    public void EnsureValidQuestions(IReadOnlyList<Question> questions)
    {
        if (questions is null || questions.Count == 0)
            throw new ArgumentException("L'esame deve contenere almeno una domanda.");

        foreach (var question in questions)
        {
            if (question is null)
                throw new ArgumentException("Una domanda non può essere nulla.");

            if (string.IsNullOrWhiteSpace(question.Text))
                throw new ArgumentException("Il testo della domanda è obbligatorio.");

            if (question.Points <= 0)
                throw new ArgumentException("Ogni domanda deve avere un punteggio maggiore di zero.");

            if (question.Position < 0)
                throw new ArgumentException("La posizione di una domanda non può essere negativa.");

            EnsureValidQuestionStructure(question);
        }

        var duplicatedPositions = questions
            .GroupBy(q => q.Position)
            .Any(g => g.Count() > 1);

        if (duplicatedPositions)
            throw new ArgumentException("Le posizioni delle domande devono essere univoche.");
    }

    public decimal CalculateTotalPoints(IReadOnlyList<Question> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);

        return questions.Sum(q => q.Points);
    }

    public void EnsureEditable(Exam exam)
    {
        ArgumentNullException.ThrowIfNull(exam);

        if (exam.Status != ExamStatus.Draft)
            throw new BusinessRuleException("La struttura dell'esame può essere modificata solo quando l'esame è in bozza.");
    }

    public void EnsurePublishable(Exam exam, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(exam);

        if (exam.Status != ExamStatus.Draft)
            throw new BusinessRuleException("Solo un esame in bozza può essere pubblicato.");

        EnsureValidName(exam.Name);

        EnsureValidSchedule(exam.AvailableFrom, exam.AvailableUntil, exam.DurationMinutes);

        EnsureValidPassingPercent(exam.PassingPercent);

        EnsureValidQuestions(exam.Questions);

        if (exam.AvailableUntil <= nowUtc)
            throw new BusinessRuleException("Non è possibile pubblicare un esame la cui disponibilità è già terminata.");

        if (exam.AssignedStudents is null || exam.AssignedStudents.Count == 0)
            throw new BusinessRuleException("L'esame deve essere assegnato ad almeno uno studente.");

        if (CalculateTotalPoints(exam.Questions) <= 0)
            throw new BusinessRuleException("Il punteggio totale dell'esame deve essere maggiore di zero.");
    }

    public void EnsureCanCancel(Exam exam, bool hasSubmissions)
    {
        ArgumentNullException.ThrowIfNull(exam);

        if (exam.Status != ExamStatus.Published)
            throw new BusinessRuleException("È possibile annullare solo un esame pubblicato.");

        if (hasSubmissions)
            throw new BusinessRuleException("Non è possibile annullare un esame già iniziato da almeno uno studente.");
    }

    public void EnsureDeletable(Exam exam, bool hasSubmissions)
    {
        ArgumentNullException.ThrowIfNull(exam);

        if (exam.Status != ExamStatus.Draft)
            throw new BusinessRuleException("È possibile eliminare solo un esame in bozza.");

        if (hasSubmissions)
            throw new BusinessRuleException("Non è possibile eliminare un esame che possiede già delle consegne.");
    }

    private static void EnsureValidQuestionStructure(Question question)
    {
        if (question.Options is null)
            throw new ArgumentException("La lista delle opzioni non può essere nulla.");

        foreach (var option in question.Options)
        {
            if (option is null)
                throw new ArgumentException("Un'opzione non può essere nulla.");

            if (string.IsNullOrWhiteSpace(option.Text))
                throw new ArgumentException("Il testo delle opzioni è obbligatorio.");

            if (option.Text.Length > 500)
                throw new ArgumentException("Il testo di un'opzione non può superare 500 caratteri.");

            if (option.Position < 0)
                throw new ArgumentException("La posizione di un'opzione non può essere negativa.");
        }

        switch (question.Type)
        {
            case QuestionType.Single:
                {
                    if (question.Options.Count < 2)
                        throw new ArgumentException("Una domanda a risposta singola deve avere almeno due opzioni.");

                    var correctCount = question.Options.Count(o => o.IsCorrect);

                    if (correctCount != 1)
                        throw new ArgumentException("Una domanda a risposta singola deve avere esattamente una risposta corretta.");

                    break;
                }

            case QuestionType.Multiple:
                {
                    if (question.Options.Count < 2)
                        throw new ArgumentException("Una domanda a risposta multipla deve avere almeno due opzioni.");

                    if (question.Options.Count(o => o.IsCorrect) < 2)
                        throw new ArgumentException("Una domanda a risposta multipla deve avere almeno due risposte corrette.");

                    break;
                }

            case QuestionType.Open:
                {
                    if (question.Options.Count > 0)
                        throw new ArgumentException("Una domanda aperta non può avere opzioni.");

                    break;
                }

            default:
                throw new ArgumentException("Tipo di domanda non valido.");
        }

        var duplicatedPositions = question.Options
            .GroupBy(o => o.Position)
            .Any(g => g.Count() > 1);

        if (duplicatedPositions)
            throw new ArgumentException("Le posizioni delle opzioni devono essere univoche.");
    }
}
