using NovaTech.TerraTech.Platform.AnalyticsManagement.Domain.Model.ValueObjects;

namespace NovaTech.TerraTech.Platform.AnalyticsManagement.Domain.Model.Commands
{
    /// <summary>
    /// Command to update an existing report.
    /// </summary>
    /// <param name="Id">Report identifier.</param>
    /// <param name="MeanValue">New mean value.</param>
    /// <param name="Variance">New variance.</param>
    /// <param name="StandardDeviation">New standard deviation.</param>
    /// <param name="TechnicalInterpretation">New technical interpretation.</param>
    public record UpdateReportCommand(
        int Id,
        double MeanValue,
        double Variance,
        double StandardDeviation,
        string TechnicalInterpretation
    );
}