using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using System.Windows.Shapes;
using System.Xml.Linq;
using ZXing;
using static System.Net.Mime.MediaTypeNames;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace MyFridgeFriend
{
    /// <summary>
    /// Interaction logic for AddItemDialog.xaml
    /// </summary>
    public partial class AddItemDialog : Window
    {
        public AddItemDialog()
        {
            InitializeComponent();
            CbxCategory.ItemsSource = Enum.GetValues(typeof(CategoryEnum)).Cast<CategoryEnum>();
            CbxCategory.SelectedIndex = 0;
            CbxStorage.ItemsSource = Globals.dbContext.StorageLocations.ToList();
        }

        private byte[] imageBytes;
        private void BtnUploadImg_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                LblStatusMessage.Text = filePath;
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

        private void BtnAddItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool isProductValid = validateProduct();

                if (isProductValid)
                {
                    byte[] imageBytes = GetImageBytesOrDefault();

                    Image uploadedImage = new Image { Image1 = imageBytes };

                    Product productToAdd = new Product
                    {
                        ProductName = TbxName.Text,
                        ActiveQuantity = 0,
                        MinimumQuantity = int.Parse(TbxMinQuantity.Text),
                        UnitOfMeasurement = TbxUnitOfMeasure.Text,
                        DateOfExpiry = DateTime.Today + TimeSpan.FromDays(1),
                        StorageLocationId = (int)CbxStorage.SelectedValue,
                        Category = (CategoryEnum)CbxCategory.SelectedValue,
                        Image = uploadedImage
                    };
                    Globals.dbContext.Products.Add(productToAdd);
                    Globals.dbContext.SaveChanges();
                    this.DialogResult = true;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }

        }

        private bool validateProduct()
        {
            string errorMessage = "";

            if (TbxName.Text == null || TbxName.Text.Length <= 2 || TbxName.Text.Length > 50)
                errorMessage += "Please add a name between 3 and 50 characters\n";
            if (TbxMinQuantity.Text.Length != 0)
            {
                bool isMinQuantityInt = int.TryParse(TbxMinQuantity.Text, out int MinQuantityInt);
                if (isMinQuantityInt == false)
                    errorMessage += "The minimum quantity must be an integer\n";
                else if (MinQuantityInt < 0)
                    errorMessage += "The minimum quantity must be greater than 0\n";
            }
            if (!(TbxUnitOfMeasure.Text.Length > 1))
                errorMessage += "Please enter a unit of measure\n";
            if (TbxNotes.Text.Length > 200)
                errorMessage += "Notes can be a maximum of 200 characters\n";
            if (!(TbxUpc.Text.Length == 0))
            {
                if (!(TbxUpc.Text.Length == 12))
                    errorMessage += "UPC must be 12 digits long";
            }

            if (errorMessage != "")
            {
                System.Windows.MessageBox.Show(errorMessage);
                return false;
            }

            return true;
        }

        private byte[] GetImageBytesOrDefault()
        {
            if (imageBytes != null)
            {
                return imageBytes;
            }
            else
            {
                string defaultImagePath = "pack://application:,,,/assets/images/default-image/default-image.png";
                Uri resourceUri = new Uri(defaultImagePath, UriKind.RelativeOrAbsolute);
                StreamResourceInfo streamInfo = System.Windows.Application.GetResourceStream(resourceUri);

                if (streamInfo != null)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        streamInfo.Stream.CopyTo(memoryStream);
                        return memoryStream.ToArray();
                    }
                }
                else
                {
                    throw new FileNotFoundException("Default image not found.");
                }
            }
        }

        private void BtnScanBarcode_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpeg;*.jpg;*.bmp)|*.png;*.jpeg;*.jpg;*.bmp",
                Title = "Select a Barcode Image"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filename = openFileDialog.FileName;
                BitmapImage bitmapImage = new BitmapImage(new Uri(filename));

                // Convert BitmapImage to Bitmap
                Bitmap bitmap = BitmapImage2Bitmap(bitmapImage);

                IBarcodeReader reader = new BarcodeReader();
                var result = reader.Decode(bitmap);
                if (result != null)
                {
                    GetInfoFromUPC(result.Text);
                }
                else
                {
                    System.Windows.MessageBox.Show("Error reading barcode.Please try again.");
                }
            }
        }

        private Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }

        // TODO: Seperate logic from view manipulation
        // Code from: www.upcitemdb.com/wp/docs/main/development/getting-started/
        public void GetInfoFromUPC(string upc)
        {
            try
            {
                var client = new RestClient("https://api.upcitemdb.com/prod/trial/");
                // lookup request with GET
                var request = new RestRequest("lookup", Method.Get);

                request.AddQueryParameter("upc", upc);
                RestResponse response = client.Execute(request);
                // parsing json 
                var obj = JsonConvert.DeserializeObject<JObject>(response.Content);

                if (!obj["items"].Any())
                {
                    System.Windows.MessageBox.Show("Product info not found");
                }
                else
                {
                    //  System.Windows.MessageBox.Show("items: " + obj["items"]);
                    var firstItem = obj["items"][0];
                    TbxName.Text = firstItem["title"].ToString();
                    TbxUpc.Text = firstItem["upc"].ToString();
                    TbxNotes.Text = firstItem["description"].ToString();
                }
            }
            catch
            {
                System.Windows.MessageBox.Show("Error fetching UPC data");
            }
        }
    }
}
