using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinkOS.Plugin.Abstractions;
using Xamarin.Forms;
using LinkOS.Plugin;

namespace dummy
{
    public partial class mainPage : ContentPage
    {
        protected IDiscoveredPrinter myPrinter;
        public delegate void ChoosePrinterHandler();
        public static event ChoosePrinterHandler OnChoosePrinterChosen;
        public delegate void AlertHandler(string message, string title);
        public static event AlertHandler OnAlert;
        public delegate void ErrorHandler(string message);
        public static event ErrorHandler OnErrorAlert;
        public mainPage()
        {
            InitializeComponent();
        }
         void create_view(object sender, EventArgs args)
        {
            try
            {
                printBarcode();
            }
            catch(Exception e) { DisplayAlert("Error", e.Message, "ok"); }
        }

        private void printBarcode()
        {
            if (CheckPrinter())
            {
                new Task(new Action(() => {
                    PrintLineMode();
                })).Start();
            }
        }
        protected bool CheckPrinter()
        {
            if (null == myPrinter)
            {
                DisplayAlert("Error","Please Select a printer","ok");
                SelectPrinter();
                return false;
            }
            return true;
        }
        protected void SelectPrinter()
        {
            if (OnChoosePrinterChosen != null)
                OnChoosePrinterChosen();
        }
        private void PrintLineMode()
        {
            IConnection connection = null;
            try
            {
                connection = myPrinter.Connection;
                connection.Open();
                IZebraPrinter printer = ZebraPrinterFactory.Current.GetInstance(connection);
                if ((!CheckPrinterLanguage(connection)) || (!PreCheckPrinterStatus(printer)))
                {
                    return;
                }
                sendZplReceipt(connection);
                if (PostPrintCheckStatus(printer))
                {
                    //ShowAlert("Receipt printed.");
                }
            }
            catch (Exception ex)
            {
                // Connection Exceptions and issues are caught here
                DisplayAlert("Error",ex.Message,"ok");
            }
            finally
            {
                if ((connection != null) && (connection.IsConnected))
                    connection.Close();
                
            }
        }
        protected void ShowErrorAlert(string message)
        {
            if (OnErrorAlert != null)
                OnErrorAlert(message);
        }
        protected void ShowAlert(string message, string title)
        {
            if (OnAlert != null)
                OnAlert(message, title);
        }
        protected static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length];
            bytes = Encoding.UTF8.GetBytes(str);
            return bytes;
        }
        protected bool CheckPrinterLanguage(IConnection connection)
        {
            if (!connection.IsConnected)
                connection.Open();
            //  Check the current printer language
            byte[] response = connection.SendAndWaitForResponse(GetBytes("! U1 getvar \"device.languages\"\r\n"), 500, 100);
            string language = Encoding.UTF8.GetString(response, 0, response.Length);
            if (language.Contains("line_print"))
            {
                ShowAlert("Switching printer to ZPL Control Language.", "Notification");
            }
            // printer is already in zpl mode
            else if (language.Contains("zpl"))
            {
                return true;
            }

            //  Set the printer command languege
            connection.Write(GetBytes("! U1 setvar \"device.languages\" \"zpl\"\r\n"));
            response = connection.SendAndWaitForResponse(GetBytes("! U1 getvar \"device.languages\"\r\n"), 500, 100);
            language = Encoding.UTF8.GetString(response, 0, response.Length);
            if (!language.Contains("zpl"))
            {
                ShowErrorAlert("Printer language not set. Not a ZPL printer.");
                return false;
            }
            return true;
        }
        protected bool PreCheckPrinterStatus(IZebraPrinter printer)
        {
            // Check the printer status
            IPrinterStatus status = printer.CurrentStatus;
            if (!status.IsReadyToPrint)
            {
                ShowErrorAlert("Unable to print. Printer is " + status.Status);
                return false;
            }
            return true;
        }
        protected bool PostPrintCheckStatus(IZebraPrinter printer)
        {
            // Check the status again to verify print happened successfully
            IPrinterStatus status = printer.CurrentStatus;
            // Wait while the printer is printing
            while ((status.NumberOfFormatsInReceiveBuffer > 0) && (status.IsReadyToPrint))
            {
                status = printer.CurrentStatus;
            }
            // verify the print didn't have errors like running out of paper
            if (!status.IsReadyToPrint)
            {
                ShowErrorAlert("Error durring print. Printer is " + status.Status);
                return false;
            }
            return true;
        }
        private void sendZplReceipt(IConnection printerConnection)
        {
            /*
             This routine is provided to you as an example of how to create a variable length label with user specified data.
             The basic flow of the example is as follows

                Header of the label with some variable data
                Body of the label
                    Loops thru user content and creates small line items of printed material
                Footer of the label

             As you can see, there are some variables that the user provides in the header, body and footer, and this routine uses that to build up a proper ZPL string for printing.
             Using this same concept, you can create one label for your receipt header, one for the body and one for the footer. The body receipt will be duplicated as many items as there are in your variable data

             */

            String tmpHeader =
                    /*
                     Some basics of ZPL. Find more information here : http://www.zebra.com

                     ^XA indicates the beginning of a label
                     ^PW sets the width of the label (in dots)
                     ^MNN sets the printer in continuous mode (variable length receipts only make sense with variably sized labels)
                     ^LL sets the length of the label (we calculate this value at the end of the routine)
                     ^LH sets the reference axis for printing. 
                        You will notice we change this positioning of the 'Y' axis (length) as we build up the label. Once the positioning is changed, all new fields drawn on the label are rendered as if '0' is the new home position
                     ^FO sets the origin of the field relative to Label Home ^LH
                     ^A sets font information 
                     ^FD is a field description
                     ^GB is graphic boxes (or lines)
                     ^B sets barcode information
                     ^XZ indicates the end of a label
                     */

                    "^XA" +

                    "^POI^PW400^MNN^LL325^LH0,0" + "\r\n" +

                    "^FO50,50" + "\r\n" + "^A0,N,70,70" + "\r\n" + "^FD Shipping^FS" + "\r\n" +

                    "^FO50,130" + "\r\n" + "^A0,N,35,35" + "\r\n" + "^FDPurchase Confirmation^FS" + "\r\n" +

                    "^FO50,180" + "\r\n" + "^A0,N,25,25" + "\r\n" + "^FDCustomer:^FS" + "\r\n" +

                    "^FO225,180" + "\r\n" + "^A0,N,25,25" + "\r\n" + "^FDAcme Industries^FS" + "\r\n" +

                    "^FO50,220" + "\r\n" + "^A0,N,25,25" + "\r\n" + "^FDDelivery Date:^FS" + "\r\n" +

                    "^FO225,220" + "\r\n" + "^A0,N,25,25" + "\r\n" + "^FD{0}^FS" + "\r\n" +

                    "^FO50,273" + "\r\n" + "^A0,N,30,30" + "\r\n" + "^FDItem^FS" + "\r\n" +

                    "^FO280,273" + "\r\n" + "^A0,N,25,25" + "\r\n" + "^FDPrice^FS" + "\r\n" +

                    "^FO50,300" + "\r\n" + "^GB350,5,5,B,0^FS" + "^XZ";

            int headerHeight = 325;

            DateTime date = new DateTime();
            string sdf = "yyyy/MM/dd";
            string dateString = date.ToString(sdf);

            string header = string.Format(tmpHeader, dateString);

            printerConnection.Write(GetBytes(header));

            int heightOfOneLine = 40;

            Double totalPrice = 0;

            Dictionary<string, string> itemsToPrint = createListOfItems();

            foreach (string productName in itemsToPrint.Keys)
            {
                string price;
                itemsToPrint.TryGetValue(productName, out price);

                String lineItem = "^XA^POI^LL40" + "^FO50,10" + "\r\n" + "^A0,N,28,28" + "\r\n" + "^FD{0}^FS" + "\r\n" + "^FO280,10" + "\r\n" + "^A0,N,28,28" + "\r\n" + "^FD${1}^FS" + "^XZ";
                Double tempPrice;
                Double.TryParse(price, out tempPrice);
                totalPrice += tempPrice;
                String oneLineLabel = String.Format(lineItem, productName, price);

                printerConnection.Write(GetBytes(oneLineLabel));

            }

            long totalBodyHeight = (itemsToPrint.Count + 1) * heightOfOneLine;

            long footerStartPosition = headerHeight + totalBodyHeight;

            string tPrice = Convert.ToString(Math.Round((totalPrice), 2));

            String footer = String.Format("^XA^POI^LL600" + "\r\n" +

            "^FO50,1" + "\r\n" + "^GB350,5,5,B,0^FS" + "\r\n" +

            "^FO50,15" + "\r\n" + "^A0,N,40,40" + "\r\n" + "^FDTotal^FS" + "\r\n" +

            "^FO175,15" + "\r\n" + "^A0,N,40,40" + "\r\n" + "^FD${0}^FS" + "\r\n" +

            "^FO50,130" + "\r\n" + "^A0,N,45,45" + "\r\n" + "^FDPlease Sign Below^FS" + "\r\n" +

            "^FO50,190" + "\r\n" + "^GB350,200,2,B^FS" + "\r\n" +

            "^FO50,400" + "\r\n" + "^GB350,5,5,B,0^FS" + "\r\n" +

            "^FO50,420" + "\r\n" + "^A0,N,30,30" + "\r\n" + "^FDThanks for choosing us!^FS" + "\r\n" +

            "^FO50,470" + "\r\n" + "^B3N,N,45,Y,N" + "\r\n" + "^FD0123456^FS" + "\r\n" + "^XZ", tPrice);

            printerConnection.Write(GetBytes(footer));

        }
        private Dictionary<string, string> createListOfItems()
        {
            String[] names = { "Microwave Oven", "Sneakers (Size 7)", "XL T-Shirt", "Socks (3-pack)", "Blender", "DVD Movie" };
            String[] prices = { "79.99", "69.99", "39.99", "12.99", "34.99", "16.99" };
            Dictionary<string, string> retVal = new Dictionary<string, string>();

            for (int ix = 0; ix < names.Length; ix++)
            {
                retVal.Add(names[ix], prices[ix]);
            }
            return retVal;
        }
        async void re_print_view(object sender, EventArgs args)
        {
            await DisplayAlert("message", "Re Print", "ok");
        }
    }
}
