namespace ElectronicRegisterAPI.Domain.Enums;

// Separate from legacy UserRole. Future roles require an explicit schema/authorization change.
public enum InstitutionRole { InstitutionAdmin, Teacher, Student }
