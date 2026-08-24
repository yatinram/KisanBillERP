using System.Windows;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class FarmerDetailsDialog : Window
    {
        public FarmerDetailsDialog(Farmer f)
        {
            InitializeComponent();
            if (f != null)
            {
                TxtName.Text = f.FarmerName;
                TxtMobile.Text = f.MobileNumber;
                TxtVillage.Text = f.VillageName;
                TxtStatus.Text = f.Status == 1 ? "● Active" : "● Inactive";
            }
        }
    }
}
