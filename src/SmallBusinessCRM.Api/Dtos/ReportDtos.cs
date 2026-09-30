namespace SmallBusinessCRM.Api.Dtos;

public class DashboardReportDto
{
    public int TotalLeads { get; set; }
    public decimal OpenPipeline { get; set; }
    public decimal ConversionRate { get; set; }
    public int OverdueTasks { get; set; }
    public List<WorkloadDto> Workload { get; set; } = new();
    public List<PipelineStageDto> Pipeline { get; set; } = new();
}

public class WorkloadDto
{
    public string User { get; set; } = string.Empty;
    public int Leads { get; set; }
}

public class PipelineStageDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Value { get; set; }
}

public class AnalyticsReportDto
{
    public List<DealVelocityDto> MonthlyDealVelocity { get; set; } = new();
    public List<FunnelStageDto> LeadConversionFunnel { get; set; } = new();
    public LostDealsDto LostDeals { get; set; } = new();
    public List<ActivityDistributionDto> SalesActivityDistribution { get; set; } = new();
}

public class DealVelocityDto
{
    public string Month { get; set; } = string.Empty;
    public int CreatedCount { get; set; }
    public decimal CreatedValue { get; set; }
    public int WonCount { get; set; }
    public decimal WonValue { get; set; }
}

public class FunnelStageDto
{
    public string Stage { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Value { get; set; }
    public decimal Percentage { get; set; }
}

public class LostDealsDto
{
    public int Count { get; set; }
    public decimal Value { get; set; }
}

public class ActivityDistributionDto
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}
