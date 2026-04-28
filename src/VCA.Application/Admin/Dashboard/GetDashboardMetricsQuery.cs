using MediatR;
using VCA.Application.Admin.Common;

namespace VCA.Application.Admin.Dashboard;

public sealed record GetDashboardMetricsQuery() : IRequest<DashboardMetricsDto>;
