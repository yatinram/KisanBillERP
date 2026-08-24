using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using KrushiBillERP.Data;
using KrushiBillERP.Models;

namespace KrushiBillERP.Views
{
    public partial class FarmerDialog : Window
    {
        private Farmer _farmer;
        public FarmerDialog(Farmer farmer = null)
        {
            InitializeComponent();
            _farmer = farmer ?? new Farmer();
            // set window title according to mode
            this.Title = (_farmer != null && _farmer.FarmerId != 0) ? "Edit Farmer" : "Add Farmer";
            LoadData();
        }

        private void LoadData()
        {
            if (_farmer == null) return;
            TxtName.Text = _farmer.FarmerName;
            TxtMobile.Text = _farmer.MobileNumber;
            TxtVillage.Text = _farmer.VillageName;
          
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = TxtName.Text?.Trim();
                var mobile = TxtMobile.Text?.Trim();
                var village = TxtVillage.Text?.Trim();
               

                if (string.IsNullOrWhiteSpace(name) || name.Length < 2) throw new Exception("Farmer name is required and must be at least 2 characters.");
                // Do not allow name that is only numbers
                if (name.All(char.IsDigit)) throw new Exception("Farmer name cannot be numbers only.");

                if (string.IsNullOrWhiteSpace(mobile)) throw new Exception("Mobile number is required.");
                if (mobile.Length != 10 || !mobile.All(char.IsDigit)) throw new Exception("Mobile must be exactly 10 digits.");
                if (!"6789".Contains(mobile[0])) throw new Exception("Mobile must start with 6,7,8 or 9.");

                if (string.IsNullOrWhiteSpace(village) || village.Length < 2) throw new Exception("Village name is required and must be at least 2 characters.");

                // Duplicate mobile check
                if (DatabaseHelper.IsFarmerMobileExists(mobile, _farmer.FarmerId))
                {
                    MessageBox.Show("A farmer with this mobile number already exists.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _farmer.FarmerName = name;
                _farmer.MobileNumber = mobile;
                _farmer.VillageName = village;
               
                // If Farmer class later adds Notes, you can store notes; for now ignore or extend model
                _farmer.UpdatedDate = DateTime.Now;

                if (_farmer.FarmerId == 0) _farmer.CreatedDate = DateTime.Now;

                DatabaseHelper.SaveFarmer(_farmer);
                MessageBox.Show(_farmer.FarmerId == 0 ? "Farmer added successfully." : "Farmer updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Validation / Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
