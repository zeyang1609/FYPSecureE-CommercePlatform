using FYP.Models.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Linq;

using System.Threading.Tasks;

namespace FYP.Services
{
    public interface IPdfReceiptService
    {
        Task<byte[]> GenerateReceiptAsync(Order order, string receiptNumber);
    }

    public class PdfReceiptService : IPdfReceiptService
    {
        private readonly IShippingService _shippingService;

        public PdfReceiptService(IShippingService shippingService)
        {
            _shippingService = shippingService;
        }

        public async Task<byte[]> GenerateReceiptAsync(Order order, string receiptNumber)
        {
            decimal originalFee = 0;
            decimal finalFee = 0;

            if (order.OrderItems != null && order.OrderItems.Any() && order.Buyer?.Addresses != null)
            {
                var defaultAddress = order.Buyer.Addresses.FirstOrDefault(a => a.IsDefault) 
                                     ?? order.Buyer.Addresses.FirstOrDefault();
                if (defaultAddress != null)
                {
                    var addressString = $"{defaultAddress.HouseBuildingStreet}, {defaultAddress.StateArea} {defaultAddress.PostalCode}";
                    var shippingItems = order.OrderItems.Select(i => (i.ProductID, i.Quantity));
                    var subtotal = order.OrderItems.Sum(i => i.UnitPrice * i.Quantity);
                    
                    var shippingResult = await _shippingService.CalculateAndAssignShippingAsync(shippingItems, subtotal, addressString, null);
                    originalFee = shippingResult.OriginalFee;
                    finalFee = shippingResult.FinalFee;
                }
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => ComposeHeader(c, order));
                    page.Content().Element(c => ComposeContent(c, order, originalFee, finalFee, receiptNumber));
                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, Order order)
        {
            var sellerName = order.OrderItems?.FirstOrDefault()?.Product?.Seller?.StoreName 
                          ?? order.OrderItems?.FirstOrDefault()?.Product?.Seller?.Name 
                          ?? "Seller Name";

            container.Row(row =>
            {
                row.RelativeItem().Text(sellerName)
                    .FontSize(20)
                    .SemiBold();

                row.RelativeItem().AlignRight().Text("ORDER RECEIPT")
                    .FontSize(20)
                    .FontColor(Colors.Grey.Lighten1)
                    .SemiBold();
            });
        }

        private void ComposeContent(IContainer container, Order order, decimal originalFee, decimal finalFee, string receiptNumber)
        {
            container.PaddingVertical(1, Unit.Centimetre).Column(column =>
            {
                column.Spacing(20);

                // Customer Info Box
                column.Item().Background(Colors.Grey.Lighten4).Padding(10).Grid(grid =>
                {
                    grid.VerticalSpacing(5);
                    grid.HorizontalSpacing(15);
                    grid.Columns(2);

                    var buyerName = order.Buyer?.Name ?? order.BuyerID;
                    var defAddr = order.Buyer?.Addresses?.FirstOrDefault(a => a.IsDefault);
                    var address = defAddr != null 
                        ? $"{defAddr.HouseBuildingStreet}, {defAddr.StateArea} {defAddr.PostalCode}" 
                        : "N/A";

                    grid.Item().Text(t => { t.Span("Customer Name: ").SemiBold(); t.Span(buyerName); });
                    grid.Item().Text(t => { t.Span("Receipt Number: ").SemiBold(); t.Span(receiptNumber); });
                    
                    grid.Item().Text(t => { t.Span("Customer Address: ").SemiBold(); t.Span(address); });
                    grid.Item().Text(t => { t.Span("Receipt Date: ").SemiBold(); t.Span(DateTime.Now.ToString("dd/MM/yyyy")); });
                });

                // Order Info Row (Order ID and Order Paid Date)
                var paidDate = order.Payment?.CreatedAt.ToString("dd/MM/yyyy") ?? order.CreatedAt.ToString("dd/MM/yyyy");
                column.Item().Grid(grid => 
                {
                    grid.Columns(2);
                    grid.Item().Text(t => { t.Span("Order ID: ").SemiBold(); t.Span(order.OrderID); });
                    grid.Item().Text(t => { t.Span("Order Paid Date: ").SemiBold(); t.Span(paidDate); });
                });

                // Payment Method Row
                var paymentMethod = order.Payment?.PaymentMethod ?? "Unknown";
                column.Item().Text(t => { t.Span("Payment Method: ").SemiBold(); t.Span(paymentMethod); });

                // Order Details Table
                column.Item().Text("Order Details").FontSize(14).SemiBold();
                column.Item().Element(c => ComposeTable(c, order));

                // Final Financial Breakdown
                column.Item().AlignRight().Element(c => ComposeFinancialBreakdown(c, order, originalFee, finalFee));
            });
        }

        private void ComposeTable(IContainer container, Order order)
        {
            container.Table(table =>
            {
                // Define columns
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.ConstantColumn(30);
                    columns.RelativeColumn();
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Text("No.");
                    header.Cell().Text("Product");
                    header.Cell().Text("Variation");
                    header.Cell().Text("Net Product Price");
                    header.Cell().AlignCenter().Text("Qty");
                    header.Cell().AlignRight().Text("Subtotal");
                    
                    header.Cell().ColumnSpan(6)
                        .PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                });

                // Rows
                if (order.OrderItems != null)
                {
                    int index = 1;
                    decimal totalProductSubtotal = 0;
                    int totalQuantity = 0;

                    foreach (var item in order.OrderItems)
                    {
                        var subtotal = item.UnitPrice * item.Quantity;
                        totalProductSubtotal += subtotal;
                        totalQuantity += item.Quantity;

                        table.Cell().Element(CellStyle).Text($"{index}");
                        table.Cell().Element(CellStyle).Text(item.Product?.Title ?? "Unknown Product");
                        table.Cell().Element(CellStyle).Text("Standard"); // We don't have variation logic currently
                        table.Cell().Element(CellStyle).Text($"{item.UnitPrice:0.00}");
                        table.Cell().Element(CellStyle).AlignCenter().Text($"{item.Quantity}");
                        table.Cell().Element(CellStyle).AlignRight().Text($"{subtotal:0.00}");

                        index++;
                    }

                    // Table Summary
                    table.Cell().ColumnSpan(6).PaddingVertical(10).AlignRight().Row(r => 
                    {
                        r.RelativeItem().AlignRight().Column(c => 
                        {
                            c.Item().Text("Subtotal").SemiBold();
                            c.Item().Text("Total Quantity (Active)");
                        });
                        r.ConstantItem(100).AlignRight().Column(c => 
                        {
                            c.Item().Text($"RM {totalProductSubtotal:0.00}").SemiBold();
                            c.Item().Text($"{totalQuantity} items");
                        });
                    });
                }

                static IContainer CellStyle(IContainer container)
                {
                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                }
            });
        }

        private void ComposeFinancialBreakdown(IContainer container, Order order, decimal originalFee, decimal finalFee)
        {
            // Calculating breakdown
            decimal merchandiseSubtotal = 0;
            if (order.OrderItems != null)
            {
                merchandiseSubtotal = order.OrderItems.Sum(i => i.UnitPrice * i.Quantity);
            }

            decimal shippingDiscount = originalFee - finalFee;
            if (shippingDiscount < 0) shippingDiscount = 0;

            container.Background(Colors.Grey.Lighten4).Padding(15).Width(250).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Merchandise Subtotal");
                    row.ConstantItem(80).AlignRight().Text($"RM {merchandiseSubtotal:0.00}");
                });

                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("Shipping Fee").FontColor(Colors.Blue.Darken2);
                    row.ConstantItem(80).AlignRight().Text($"{originalFee:0.00}").FontColor(Colors.Blue.Darken2);
                });

                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("Shipping Discount Subtotal").FontColor(Colors.Blue.Darken2);
                    row.ConstantItem(80).AlignRight().Text($"-{shippingDiscount:0.00}").FontColor(Colors.Blue.Darken2);
                });

                column.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Total Paid").SemiBold();
                    row.ConstantItem(80).AlignRight().Text($"RM {order.TotalAmount:0.00}").SemiBold();
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(x =>
            {
                x.Span("Page ");
                x.CurrentPageNumber();
                x.Span(" of ");
                x.TotalPages();
            });
        }
    }
}
