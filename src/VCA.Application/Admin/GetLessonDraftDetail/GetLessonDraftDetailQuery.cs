using MediatR;
using VCA.Application.Admin.Common;
using VCA.Domain.Common;

namespace VCA.Application.Admin.GetLessonDraftDetail;

public sealed record GetLessonDraftDetailQuery(Guid LessonId) : IRequest<Result<LessonDraftDetailDto>>;
