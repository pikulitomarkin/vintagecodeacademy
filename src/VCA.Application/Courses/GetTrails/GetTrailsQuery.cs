namespace VCA.Application.Courses.GetTrails;

/// <summary>
/// Query para listar trilhas publicadas disponíveis para os alunos.
/// </summary>
public record GetTrailsQuery(bool PublishedOnly = true);
