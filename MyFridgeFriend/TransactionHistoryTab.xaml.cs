using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Interaction logic for TransactionHistoryTab.xaml
    /// </summary>
    public partial class TransactionHistoryTab : UserControl
    {
        public TransactionHistoryTab()
        {
            InitializeComponent();
            Globals.dbContext = new FridgeFriendDbConnection();
            PopulateTransactionHistory();
        }

        public void PopulateTransactionHistory()
        {
            try
            {
                var transactions = Globals.dbContext.Transactions
                    .OrderByDescending(t => t.DateAndTime)
                    .ToList();

                // Populate the ListView with transaction data
                LvTransactionHistory.ItemsSource = transactions;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Environment.Exit(1);
            }


        }
    }
}
