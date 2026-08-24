using System.Windows;
using System.Windows.Documents;

namespace KrushiBillERP.Views
{
    public partial class PaymentReceiptPreviewWindow : Window
    {
        private int _receiptId;
        private FlowDocument _doc;

        public PaymentReceiptPreviewWindow(int receiptId)
        {
            InitializeComponent();
            _receiptId = receiptId;
            LoadDocument();
        }

        private void LoadDocument()
        {
            _doc = PaymentReceiptPrintHelper.GetReceiptFlowDocument(_receiptId);
            if (_doc != null)
            {
                DocViewer.Document = _doc;
            }
            else
            {
                MessageBox.Show("Unable to load receipt preview.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Controls.PrintDialog();
            if (dlg.ShowDialog() == true && _doc != null)
            {
                _doc.PagePadding = new Thickness(40);
                _doc.ColumnWidth = dlg.PrintableAreaWidth;
                IDocumentPaginatorSource idp = _doc;
                dlg.PrintDocument(idp.DocumentPaginator, "Payment Receipt");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
