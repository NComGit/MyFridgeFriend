using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MyFridgeFriend;

namespace MyFridgeFriend
{
    /// <summary>
    /// Interaction logic for EditItemDialog.xaml
    /// </summary>
    public partial class EditItemDialog : Window
    {
        private Product _selectedProduct;
        private byte[] imageBytes;


        public EditItemDialog(Product product)
        {
            InitializeComponent();
            CbxCategory.ItemsSource = Enum.GetValues(typeof(CategoryEnum)).Cast<CategoryEnum>();
            CbxStorage.ItemsSource = Globals.dbContext.StorageLocations.ToList();
            _selectedProduct = product; // Assign the product to the private field
            PopulateFields(); // Call PopulateFields to pre-populate the fields with product data

        }

        private void PopulateFields()
        {
            try
            {
                // Populate the fields with the selected product data
                TbxName.Text = _selectedProduct.ProductName;
                TbxActiveQuantity.Text = _selectedProduct.ActiveQuantity.ToString();
                TbxMinQuantity.Text = _selectedProduct.MinimumQuantity?.ToString() ?? "";
                TbxUnitOfMeasure.Text = _selectedProduct.UnitOfMeasurement;
                TbxUpc.Text = _selectedProduct.UPC;
                TbxNotes.Text = _selectedProduct.Notes;
                CalDateOfExpiry.SelectedDate = _selectedProduct.DateOfExpiry;
                // Set ComboBox items source and select the correct item based on StorageLocationId
                CbxStorage.ItemsSource = Globals.dbContext.StorageLocations.ToList();
                CbxStorage.SelectedValue = _selectedProduct.StorageLocationId;
                CbxCategory.SelectedValue = _selectedProduct.Category;

                // Display the image if available
                if (_selectedProduct.Image != null && _selectedProduct.Image.Image1 != null)
                {
                    DisplayImage(_selectedProduct.Image.Image1);
                }
                else
                {
                    LblStatusMessage.Text = "No Image";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error populating fields: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayImage(byte[] imageData)
        {
            try
            {

                if (imageData == null || imageData.Length == 0 || imageData.Length == 3)
                {
                    // If image data is null, update the label and return without attempting to display the image
                    LblStatusMessage.Text = "No Image";
                    return;
                }

                // Convert byte array to BitmapImage
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = new MemoryStream(imageData);
                bitmapImage.EndInit();

                // Set BitmapImage as the source for the Image control
                ImgProduct.Source = bitmapImage;

                // Update the image upload label
                LblStatusMessage.Text = "Image Loaded";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnUploadImg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                if (openFileDialog.ShowDialog() == true)
                {
                    string filePath = openFileDialog.FileName;
                    LblStatusMessage.Text = "New Image Loaded";

                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        using (BinaryReader br = new BinaryReader(fs))
                        {
                            imageBytes = br.ReadBytes((int)fs.Length);

                            // Display the newly loaded image
                            DisplayImage(imageBytes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void BtnEditItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool isProductValid = ValidateProduct();

                if (isProductValid)
                {
                    // Update the selected product properties based on user input
                    var oldActiveQuantity = _selectedProduct.ActiveQuantity;
                    _selectedProduct.ProductName = TbxName.Text;
                    _selectedProduct.ActiveQuantity = int.Parse(TbxActiveQuantity.Text);
                    _selectedProduct.MinimumQuantity = string.IsNullOrEmpty(TbxMinQuantity.Text) ? null : (int?)int.Parse(TbxMinQuantity.Text);
                    _selectedProduct.UnitOfMeasurement = TbxUnitOfMeasure.Text;
                    _selectedProduct.DateOfExpiry = CalDateOfExpiry.SelectedDate.HasValue
                        ? CalDateOfExpiry.SelectedDate.Value
                        : DateTime.MaxValue;
                    _selectedProduct.StorageLocationId = (int)CbxStorage.SelectedValue;
                    _selectedProduct.Category = (CategoryEnum)CbxCategory.SelectedValue;
                    _selectedProduct.UPC = TbxUpc.Text;
                    _selectedProduct.Notes = TbxNotes.Text;

                    // Update the image if a new one is uploaded
                    if (imageBytes != null)
                    {
                        _selectedProduct.Image = new Image { Image1 = imageBytes };
                    }

                    // Check if the active quantity has changed
                    if (_selectedProduct != null && int.TryParse(TbxActiveQuantity.Text, out int newQuantity))
                    {

                        if (oldActiveQuantity != newQuantity)
                        {
                            // Record a transaction for the quantity change
                            RecordTransaction(_selectedProduct, oldActiveQuantity, newQuantity);
                        }
                    }

                    // Save changes to the database
                    Globals.dbContext.SaveChanges();

                    // Close the dialog
                    this.DialogResult = true;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error editing item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecordTransaction(Product product, int oldQuantity, int newQuantity)
        {
            // Create a new transaction object
            Transaction transaction = new Transaction
            {
                ProductId = product.Id,
                Quantity = newQuantity - oldQuantity, // Calculate the change in quantity
                DateAndTime = DateTime.Now
            };

            // Add the transaction to the database context
            Globals.dbContext.Transactions.Add(transaction);
        }


        private bool ValidateProduct()
        {
            string errorMessage = "";


            // Product name check
            if (TbxName.Text == null || TbxName.Text.Length <= 2 || TbxName.Text.Length > 50)
                errorMessage += "Please add a name between 3 and 50 characters\n";
            // Active Quantity Check
            if (!int.TryParse(TbxActiveQuantity.Text, out int activeQuantity) || activeQuantity < 0)
                errorMessage += "Active quantity must be a non-negative integer\n";
            // Min Quantity check
            if (TbxMinQuantity.Text.Length != 0)
            {
                bool isMinQuantityInt = int.TryParse(TbxMinQuantity.Text, out int MinQuantityInt);
                if (isMinQuantityInt == false)
                    errorMessage += "The minimum quantity must be an integer\n";
                else if (MinQuantityInt < 0)
                    errorMessage += "The minimum quantity must be greater than 0\n";
            }
            // Unit of Measure check
            if (!(TbxUnitOfMeasure.Text.Length > 1))
                errorMessage += "Please enter a unit of measure\n";
            // Expiry Date check
            if (!CalDateOfExpiry.SelectedDate.HasValue || CalDateOfExpiry.SelectedDate < DateTime.Today)
                errorMessage += "Please enter a valid future expiry date\n";
            // Note check
            if (TbxNotes.Text.Length > 200)
                errorMessage += "Notes can be a maximum of 200 characters\n";
            // UPC check
            if (!(TbxUpc.Text.Length == 0))
            {
                if (!(TbxUpc.Text.Length == 12))
                    errorMessage += "UPC must be 12 digits long";
            }
            if (errorMessage != "")
            {
                MessageBox.Show(errorMessage);
                return false;
            }

            return true;
        }

        private void CalDateOfExpiry_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Handle the date selection change here
                DateTime? selectedDate = CalDateOfExpiry.SelectedDate;

                if (selectedDate.HasValue)
                {
                    // Update the DateOfExpiry property of the selected product
                    _selectedProduct.DateOfExpiry = selectedDate.Value;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error handling date selection change: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to delete this item?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    // Perform deletion logic
                    Globals.dbContext.Products.Remove(_selectedProduct);
                    Globals.dbContext.SaveChanges();

                    // Close the dialog or navigate to a different page if needed
                    this.DialogResult = true;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
