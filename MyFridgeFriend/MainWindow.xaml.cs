using MaterialDesignColors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MyFridgeFriend
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<Product> _allProducts;
        private bool isFocusBasedChange = false;
        private ObservableCollection<Product> filteredProducts = new ObservableCollection<Product>();

        public MainWindow()
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

            try
            {
                Globals.dbContext = new FridgeFriendDbConnection();
                _allProducts = new ObservableCollection<Product>(Globals.dbContext.Products.ToList());
                filteredProducts = _allProducts;
                LvProducts.ItemsSource = _allProducts;
            }
            catch (SystemException ex)
            {
                MessageBox.Show(this, "Error reading from database\n" + ex.Message, "Fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            // Create an instance of the PrintDialog window
            PrintDialog printDialog = new PrintDialog();

            // Show the PrintDialog window
            printDialog.ShowDialog();
        }

        private void BtnEditItem_Click(object sender, RoutedEventArgs e)
        {
            // Check if an item is selected
            if (LvProducts.SelectedItem != null && LvProducts.SelectedItems.Count == 1)
            {
                // Create an instance of the EditItemDialog
                EditItemDialog editItemWindow = new EditItemDialog(LvProducts.SelectedItem as Product);

                // Show the EditItemDialog window
                bool? updateList = editItemWindow.ShowDialog();

                // Refresh the ListView if changes were made
                if (updateList == true)
                {
                    RefreshListViewMain();
                    LblStatusMessage.Text = "Item updated!";

                }
                    
            }
            else
            {
                MessageBox.Show("Please select one item to edit.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        private void LvProducts_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double totalWidth = 0;
            var listView = sender as ListView;
            var gridView = listView.View as GridView;

            // Subtract the width of each column except the "Notes" column
            foreach (var column in gridView.Columns)
            {
                if (column.Header.ToString() != "Notes")
                {
                    totalWidth += column.ActualWidth;
                }
            }

            // Subtract additional space for scrollbar, padding, border, etc. as needed
            double padding = 30; // Adjust this value as needed

            // Calculate the remaining width, ensuring it's not negative
            double remainingWidth = listView.ActualWidth - totalWidth - padding;
            remainingWidth = Math.Max(remainingWidth, 0); // Ensure the width is not negative

            // Set the remaining width to the "Notes" column
            gridView.Columns.First(c => c.Header.ToString() == "Notes").Width = remainingWidth-20;
        }

        private void BtnAddItem_Click(object sender, RoutedEventArgs e)
        {
            AddItemDialog newItemWindow = new AddItemDialog();
            bool? updateList = newItemWindow.ShowDialog();

            if (updateList == true)
            {
                RefreshListViewMain();
                LblStatusMessage.Text = "Item added successfully.";

            }
        }

        private void RefreshListViewMain()
        {
            _allProducts.Clear(); // Clear the existing items
            foreach (var product in Globals.dbContext.Products.ToList())
            {
                _allProducts.Add(product); // Add items from the database to the collection
            }

            // Updated filtered products and refresh list view (keeps current search/filters)
            UpdateFilteredProducts();
            LvProducts.ItemsSource = filteredProducts;

        }

        private void TbxSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null && textBox.Text == "Search by Product Name...")
            {
                isFocusBasedChange = true;
                textBox.Text = string.Empty;
                textBox.Foreground = Brushes.Black;
                isFocusBasedChange = false;
            }
        }

        private void TbxSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                isFocusBasedChange = true;
                textBox.Text = "Search by Product Name...";
                textBox.Foreground = Brushes.Gray;
                isFocusBasedChange = false; 
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            GrdMain.Focus();
        }

        private void TbxSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isFocusBasedChange)
            {
                // Ignore text changes caused by focus events
                return;
            }
            if (_allProducts == null)
            {
                // Need this or the program crashes at compilation
                return;
            }

            var searchText = ((TextBox)sender).Text.ToLower();
            var keywordFilteredProducts = string.IsNullOrWhiteSpace(searchText)
                ? filteredProducts
                : new ObservableCollection<Product>(filteredProducts.Where(p => p.ProductName.ToLower().Contains(searchText)));

            LvProducts.ItemsSource = keywordFilteredProducts;
        }

        // SORTING
        GridViewColumnHeader _lastHeaderClicked = null;
        ListSortDirection _lastDirection = ListSortDirection.Ascending;

        void GridViewColumnHeaderClickedHandler(object sender,
                                                RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked == null) return;
            if (headerClicked.Role == GridViewColumnHeaderRole.Padding) return;
            if (headerClicked != _lastHeaderClicked)
            {
                direction = ListSortDirection.Ascending;
            }
            else
            {
                direction = _lastDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            }

            var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
            var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;

            Sort(sortBy, direction);

            if (direction == ListSortDirection.Ascending)
            {
                headerClicked.Column.HeaderTemplate =
                    Resources["HeaderTemplateArrowUp"] as DataTemplate;
            }
            else
            {
                headerClicked.Column.HeaderTemplate =
                    Resources["HeaderTemplateArrowDown"] as DataTemplate;
            }

            // Remove arrow from previously sorted header
            if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
            {
                _lastHeaderClicked.Column.HeaderTemplate = null;
            }

            _lastHeaderClicked = headerClicked;
            _lastDirection = direction;
        }
        private void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView =
                CollectionViewSource.GetDefaultView(LvProducts.ItemsSource);

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }


        //SELECTION
        private void LvProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Process added items
                foreach (var selectedItem in e.AddedItems)
                {
                    // Add item to LvProducts (if not already present)
                    if (!LvProducts.SelectedItems.Contains(selectedItem))
                    {
                        LvProducts.SelectedItems.Add(selectedItem);
                    }
                }

                // Process removed items
                foreach (var removedItem in e.RemovedItems)
                {
                    // Remove item from LvProducts (if present)
                    if (LvProducts.SelectedItems.Contains(removedItem))
                    {
                        LvProducts.SelectedItems.Remove(removedItem);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error handling selection changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            // Check if at least one item is selected
            if (LvProducts.SelectedItems.Count > 0)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to delete the selected items?", "Confirm: Delete Item(s)", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Delete selected items
                    foreach (var selectedItem in LvProducts.SelectedItems.OfType<Product>().ToList())
                    {
                        // Remove from the database
                        Globals.dbContext.Products.Remove(selectedItem);

                        // Remove from the local ObservableCollection
                        _allProducts.Remove(selectedItem);
                    }

                    // Save changes to the database
                    Globals.dbContext.SaveChanges();

                    // Refresh the ListView
                    RefreshListViewMain();
                    LblStatusMessage.Text = "Item(s) deleted.";
                }
            }
            else
            {
                MessageBox.Show("Please select at least one item to delete.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Check if the selected tab is the Transaction History tab
            if (MainTabControl.SelectedItem is TabItem selectedTab && selectedTab.Header.ToString() == "Transaction History")
            {
                // Refresh the transaction history
                var transactionHistoryTab = selectedTab.Content as TransactionHistoryTab;
                transactionHistoryTab?.PopulateTransactionHistory();
            }
        }

        // FILTERS
        private FridgeFriendDbConnection dbContext = new FridgeFriendDbConnection();
        private ObservableCollection<Product> FilterProducts()
        {
            // Filter btn states determine list

            var filteredList = _allProducts.AsQueryable();

            var currentDate = DateTime.Now.Date;
            var targetDate = currentDate.AddMonths(1);

            // Filter: Location
            if (IsIncludeFreezer || IsIncludeFridge || IsIncludePantry)
            {
                filteredList = filteredList.Where(p =>
                    (IsIncludeFreezer && p.StorageLocation.StorageName == "Freezer") ||
                    (IsIncludeFridge && p.StorageLocation.StorageName == "Fridge") ||
                    (IsIncludePantry && p.StorageLocation.StorageName == "Pantry"));
            }

            // Filter: Quantity
            if (IsFullyStocked || IsRunningLow || IsOutOfStock)
            {
                filteredList = filteredList.Where(p =>
                    (IsFullyStocked && p.ActiveQuantity > p.MinimumQuantity) ||
                    (IsRunningLow && p.ActiveQuantity <= p.MinimumQuantity) ||
                    (IsOutOfStock && p.ActiveQuantity == 0));
            }

            // Filter: Expiration
            if (IsFreshOrNA || IsExpiresSoon || IsExpired)
            {
                filteredList = filteredList.Where(p =>
                    (IsFreshOrNA && p.DateOfExpiry >= targetDate) ||
                    (IsExpiresSoon && (p.DateOfExpiry >= currentDate && p.DateOfExpiry < targetDate)) ||
                    (IsExpired && p.DateOfExpiry < currentDate));
            }

            filteredProducts = new ObservableCollection<Product>(filteredList.ToList()); ;

            //foreach (var item in filteredList.ToList())
            //{
            //    Console.WriteLine($"Product Name: {item.ProductName}, Active Quantity: {item.ActiveQuantity}, Expiry: {item.DateOfExpiry.ToShortDateString()}");
            //}

            return filteredProducts;
        }

        public bool IsFreshOrNA { get; set; }

        public bool IsExpired { get; set; }

        public bool IsExpiresSoon { get; set; }

        public bool IsOutOfStock { get; set; }

        public bool IsRunningLow { get; set; }

        public bool IsFullyStocked { get; set; }

        public bool IsIncludePantry { get; set; }

        public bool IsIncludeFridge { get; set; }

        public bool IsIncludeFreezer { get; set; }

        private void UpdateFilteredProducts(object sender, RoutedEventArgs routedEventArgs)
        {
            UpdateFilteredProducts(); //overloading
        }
        private void UpdateFilteredProducts()
        {
            // Update the filtered products based on the selected options
            filteredProducts = FilterProducts();

            //update list view, with these filters AND the searchbox keyword filter (if not null/empty)
            LvProducts.ItemsSource = (string.IsNullOrWhiteSpace(TbxSearch.Text) || TbxSearch.Text == "Search by Product Name...") ? new ObservableCollection<Product>(filteredProducts) : new ObservableCollection<Product>(filteredProducts.Where(p => p.ProductName.ToLower().Contains(TbxSearch.Text)));
        }

    }
}

