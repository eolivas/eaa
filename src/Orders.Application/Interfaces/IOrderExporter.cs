using Orders.Domain;

namespace Orders.Application.Interfaces;

/// <summary>
/// Export operations for orders (ISP: separated from read and write concerns).
/// </summary>
public interface IOrderExporter
{
    /// <summary>
    /// Exports the specified order to a PDF byte array.
    /// </summary>
    Task<byte[]> ExportToPdf(OrderId orderId, CancellationToken cancellationToken = default);
}
