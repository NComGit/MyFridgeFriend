using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Forms;

namespace MyFridgeFriend
{
    public partial class PrintDialog : Window
    {
        private List<Product> filteredProducts;
        private FridgeFriendDbConnection dbContext = new FridgeFriendDbConnection();

        public bool IsIncludeFridge { get; set; }
        public bool IsIncludePantry { get; set; }
        public bool IsIncludeFreezer { get; set; }

        public bool IsFullyStocked { get; set; }
        public bool IsRunningLow { get; set; }
        public bool IsOutOfStock { get; set; }

        public bool IsFreshOrNA { get; set; }
        public bool IsExpiresSoon { get; set; }
        public bool IsExpired { get; set; }



        public PrintDialog()
        {
            InitializeComponent();

            IsIncludeFridge = false;
            IsIncludePantry = false;
            IsIncludeFreezer = false;

            IsFullyStocked = false;
            IsRunningLow = false;
            IsOutOfStock = false;

            IsFreshOrNA = false;
            IsExpiresSoon = false;
            IsExpired = false;

            DataContext = this; // Set DataContext for binding
        }

        private List<Product> FilterProducts()
        {
            // Implement filtering logic based on button states

            var filteredList = dbContext.Products.AsQueryable();
            var currentDate = DateTime.Now.Date;

            var targetDate = currentDate.AddMonths(1);

            // Apply storage name filters
            if (IsIncludeFreezer || IsIncludeFridge || IsIncludePantry)
            { 
                filteredList = filteredList.Where(p =>
                    (IsIncludeFreezer && p.StorageLocation.StorageName == "Freezer") ||
                    (IsIncludeFridge && p.StorageLocation.StorageName == "Fridge") ||
                    (IsIncludePantry && p.StorageLocation.StorageName == "Pantry"));
            }

            // Apply quantity filters
            if (IsFullyStocked || IsRunningLow || IsOutOfStock)
            {
                filteredList = filteredList.Where(p =>
                    (IsFullyStocked && p.ActiveQuantity > p.MinimumQuantity) ||
                    (IsRunningLow && p.ActiveQuantity <= p.MinimumQuantity) ||
                    (IsOutOfStock && p.ActiveQuantity == 0));
            }

            // Apply additional filters
            if (IsFreshOrNA || IsExpiresSoon || IsExpired)
            {
                filteredList = filteredList.Where(p =>
                    (IsFreshOrNA && p.DateOfExpiry >= targetDate) ||
                    (IsExpiresSoon && (p.DateOfExpiry >= currentDate && p.DateOfExpiry < targetDate)) ||
                    (IsExpired && p.DateOfExpiry < currentDate));
            }

            var list = filteredList.ToList();

            foreach (var item in list)
            {
                Console.WriteLine($"Product Name: {item.ProductName}, Active Quantity: {item.ActiveQuantity}, Expiry: {item.DateOfExpiry.ToShortDateString()}");
            }

            return filteredList.ToList();
        }

        private void UpdateFilteredProducts()
        {
            // Update the filtered products based on the selected options
            filteredProducts = FilterProducts();
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (!IsIncludeFreezer && !IsIncludeFridge && !IsIncludePantry &&
                !IsFullyStocked && !IsRunningLow && !IsOutOfStock)
            {
                // Show alert message
                System.Windows.MessageBox.Show
                    ("No filters selected. Please select at least one filter.", 
                        "Warning", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Filter products based on selected options
            filteredProducts = FilterProducts();

            // Reset pagination settings
            _currentPage = 0;

            // Create a PrintDocument
            PrintDocument printDocument = new PrintDocument();

            // Register for the PrintPage event
            printDocument.PrintPage += PrintPage;

            // Display PrintDialog to choose printer settings
            System.Windows.Forms.PrintDialog printDialog = new System.Windows.Forms.PrintDialog();
            printDialog.Document = printDocument;

            if (printDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Start printing
                printDocument.Print();
            }
        }

        private int _itemsPerPage = 55; // Number of items to print per page
        private int _currentPage = 0;   // Current page being printed

        private void PrintPage(object sender, PrintPageEventArgs e)
        {
            int itemsToPrint = filteredProducts.Count - _currentPage * _itemsPerPage;
            int itemsOnPage = Math.Min(_itemsPerPage, itemsToPrint);

            // Concatenate product details with \n for the current page
            string printedText = string.Join("\n", filteredProducts
                .Skip(_currentPage * _itemsPerPage)
                .Take(itemsOnPage)
                .Select(product =>
                    $"{product.ProductName}, Quantity: {product.ActiveQuantity}, " +
                    $"Expiry: {product.DateOfExpiry.ToShortDateString()}"));

            // Print the concatenated text
            e.Graphics.DrawString(printedText, new System.Drawing.Font("Arial", 12), 
                System.Drawing.Brushes.Black, new System.Drawing.PointF(10, 10));

            // Move to the next page if more items are remaining
            _currentPage++;

            e.HasMorePages = itemsToPrint > itemsOnPage;
        }



        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (!IsIncludeFreezer && !IsIncludeFridge && !IsIncludePantry &&
                !IsFullyStocked && !IsRunningLow && !IsOutOfStock)
            {
                // Show alert message
                System.Windows.MessageBox.Show("No filters selected. Please select at least one filter.", "Warning", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Use the existing instance to get the correct filtered list for export
            List<Product> toExport = FilterProducts();
            List<string> lines = new List<string>();

            // Add CSV header
            lines.Add("Product Name,Active Quantity,Minimum Quantity,Unit of Measurement,Date of Expiry,Storage Location,Category,UPC,Notes");

            // Add product details to CSV
            foreach (Product product in toExport)
            {
                // Enclose each field in double quotes to handle commas
                lines.Add($"\"{product.ProductName}\",\"{product.ActiveQuantity}\",\"{product.MinimumQuantity}\",\"{product.UnitOfMeasurement}\",\"{product.DateOfExpiry}\",\"{product.StorageLocation.StorageName}\",\"{product.Category}\",\"{product.UPC}\",\"{product.Notes}\"");
            }

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Write lines to CSV file
                    File.WriteAllLines(saveFileDialog.FileName, lines);
                    System.Windows.MessageBox.Show("CSV file exported successfully.", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error exporting CSV file: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void IncludeItemsButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateFilteredProducts();
        }

        private void LimitListButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateFilteredProducts();
        }

        private void AdditionalFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateFilteredProducts();
        }

        private void BtnFridge_Click(object sender, RoutedEventArgs e)
        {
            IncludeItemsButton_Click(sender, e);
        }

        private void BtnPantry_Click(object sender, RoutedEventArgs e)
        {
            IncludeItemsButton_Click(sender, e);
        }

        private void BtnFreezer_Click(object sender, RoutedEventArgs e)
        {
            IncludeItemsButton_Click(sender, e);
        }

        private void BtnFullyStocked_Click(object sender, RoutedEventArgs e)
        {
            LimitListButton_Click(sender, e);
        }

        private void BtnRunningLow_Click(object sender, RoutedEventArgs e)
        {
            LimitListButton_Click(sender, e);
        }

        private void BtnOutOfStock_Click(object sender, RoutedEventArgs e)
        {
            LimitListButton_Click(sender, e);
        }

        private void BtnFreshOrNA_Click(object sender, RoutedEventArgs e)
        {
            AdditionalFiltersButton_Click(sender, e);
        }

        private void BtnExpiresSoon_Click(object sender, RoutedEventArgs e)
        {
            AdditionalFiltersButton_Click(sender, e);
        }

        private void BtnExpired_Click(object sender, RoutedEventArgs e)
        {
            AdditionalFiltersButton_Click(sender, e);
        }

    }
}