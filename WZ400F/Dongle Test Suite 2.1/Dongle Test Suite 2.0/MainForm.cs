//History
//==========================================================================================================
// 20120307 |  2.1.1   | Nino Liu   |  Cancel comment out of thisUSB.ChipReset() and remove gui top's vn.
//---------------------------------------------------------------------------------------------------
// 20120309 |  2.1.2   | Nino Liu   |  Add counter id information by scaning textbox.
//---------------------------------------------------------------------------------------------------
// 20120309 |  2.1.3   | Nino Liu   |  Modified MAC header ID become Liteon uniquely and Setting file path.
//---------------------------------------------------------------------------------------------------
// 20120323 |  2.1.4   | Nino Liu   |  Modified MAC address rule and writed mac information into FT232R
//---------------------------------------------------------------------------------------------------
// 20120323 |  2.1.5   | Nino Liu   |  Modified MAC Header for 2 kinds facctory test mode veriosn 
//---------------------------------------------------------------------------------------------------
// 20120328 |  2.1.6   | Nino Liu   |  Modified variable that be able to better understand.  
//---------------------------------------------------------------------------------------------------
// 20120328 |  2.1.7   | Nino Liu   |  Add function to get frequency counter data and write in log
//==========================================================================================================
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;

namespace Dongle_Test_Suite_2._1
{
    public partial class MainForm : Form
    {
        Thread MainThread;
        Parameters parameters = new Parameters();
        delegate void StringParameterDelegate(string value);  //for invoking one thread from another, to change things on the screen
        delegate void IntParameterDelegate(int value);  //for invoking one thread from another, to change things on the screen
        FTDIdevice thisUSB;
        FTDIdevice referenceRadio;
        Exception yellow = new Exception();
        Exception red = new Exception();      

        public MainForm()
        {
            
            InitializeComponent();
            parameters = new Parameters();
            parameters.ReadSettingsFile();  //must happen after new parameters is built in order for settings to be saved as params

            if (parameters.testing && !parameters.loading) progressBar_overall.Maximum = 60;  //adjust progress bar for testing only case
            else progressBar_overall.Maximum = 100;

            bool refradioattached = true;
            bool counterattached = true;
            CheckConnections(ref counterattached, ref refradioattached);
            if(refradioattached && counterattached) UpdateOutputText("Welcome to the ThinkEco USB tester/loader 2.1. Please attach the dongle to be tested and scan its bottom housing MAC address label to begin.");
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (e.CloseReason == CloseReason.TaskManagerClosing) return;
            if (parameters.testing)
            {
                try
                {
                    referenceRadio.Close();
                }
                catch (Exception exc)
                {
                    UpdateOutputText(exc.Message);
                }
            }


            if (e.CloseReason == CloseReason.WindowsShutDown) return;

        }

        public void RunButton_Click(object sender, EventArgs e)
        {
            UpdateOutputText("Process has begun.");
            UpdateColorDisplay("white");
            UpdateProgressBar_Overall(0);
            UpdateProgressBar_Detail(0);

            parameters = new Parameters();
            parameters.ReadSettingsFile();  //must happen after new parameters is built in order for settings to be saved as params

            //open dialog box for filename, if option is selected
            if (!parameters.takingFWfilenamefromsettingsfile)
            {
                CancelEventArgs e2 = new CancelEventArgs();
                parameters.FWimagefilepath = openFileDialog1_FileOk(sender, e2);
                int lastslash = parameters.FWimagefilepath.LastIndexOf('\\') + 1;
                parameters.FWimagefilename = parameters.FWimagefilepath.Substring(lastslash, parameters.FWimagefilepath.Length-lastslash);
            }

            thisUSB = new FTDIdevice(parameters, this); //must happen after settings file is read

            MainThread = new Thread(new ThreadStart(MainProcess));
            MainThread.Start();
        }

        public void MainProcess()      //THIS IS THE MAIN CODE THAT RUNS WHEN THE BUTTON IS CLICKED
        {
            int BufferErrorIncrementer = 0;
            GoToForBufferingErrorRetry:
            try
            {
                Setup();
                ReadCounterID();
                CheckRequirements();                                //check that necessary stuff is plugged in
                ReadBarcode(); //come back to this                  //read scanned barcode (MAC address) from text box
                parameters.SetSerialNumber(); 
                UpdateOutputText("Opening USB port...");
                thisUSB.OpenPort(parameters.USB_under_test_pid, 6000);    //open up the usb port

                EraseDongleHW3SW2firmware();

                enableResetLine();                                  //enable control of the reset line on the dongle by applying new EEPROM settings to the FTDI chip (takes a few sec)
                if (parameters.testing) TestUSB();                  //load testing firmware to RAM and use it to run a radio test
                
                SetCrystalTrim();                                   //program the trim adjustment values for the crystal clock (depending on options set in Settings.txt)
                if (parameters.loading) LoadFirmware();             //load final firmware (first load SSL firmware to RAM and then pass the final FW to the SSL
                    UpdateOutputText("Programming final settings...");
                //now set EEPROM settings including ThinkEco strings and ID numbers and the appropriate reset/sleep line setting, depending on whether we've loaded or not.  (does not take effect until unplug/replug) 
                if (parameters.loading) thisUSB.SetEEPROMafterloading();
                else if (parameters.testing) thisUSB.SetEEPROMaftertestonly();
                thisUSB.Close();
                Finish();

                //items to only do once totally successful:
                Logger();
                UpdateProgressBar_Overall(progressBar_overall.Maximum);
                SaveSerialNumber();
                UpdateColorDisplay("green");
                if(parameters.testing && parameters.loading) UpdateOutputText("Test and load with MAC address " + parameters.MAC + " have completed successfully in "+ parameters.totaltesttime + " seconds.");
                else if (parameters.loading) UpdateOutputText("Loading  with MAC address " + parameters.MAC + " has completed successfully in " + parameters.totaltesttime + " seconds.");
                else UpdateOutputText("Testing  with MAC address " + parameters.MAC + " has completed successfully in " + parameters.totaltesttime + " seconds.");
            }
            catch (Exception_Yellow e)
            {
                ExceptionHandler(e, "yellow");
            }
            catch (Exception_Red e)
            {
                ExceptionHandler(e, "red");
            }
            catch (Exception e)
            {
                if (e.Message.Contains("supplied buffer is not big enough"))
                {
                    BufferErrorIncrementer++;
                    if (BufferErrorIncrementer > 3) Output.Text = "Persistent buffering error -- unplug and replug dongle and try again.  If error persists, dongle fails.";
                    else
                    {
                        Output.Text = "Buffering error -- restarting after 2 seconds.";
                        UpdateColorDisplay("orange");
                        System.Windows.Forms.Application.DoEvents();
                        System.Threading.Thread.Sleep(2000);
                        goto GoToForBufferingErrorRetry;
                    }
                }
                ExceptionHandler(e, "generic");
            }
        }

        public void ExceptionHandler(Exception ex, string category)
        {
            try
            {
                thisUSB.SetEEPROMaftererror();    //temp useful for debugging if there are write issues, avoids using FT_prog if program stops before SetEEPROM. But causes trouble if there is no board attached.
                thisUSB.Close();
            }
            catch(Exception ee){}

            Finish();
            if (ex.Message.Contains("GPIB") || ex.Message.Contains("handle is out of range"))
            {
                ex = new Exception_Yellow("Error involving the frequency counter or GPIB connection (" + ex.Message + " 2).  Confirm that the GPIB cable is plugged into both the computer and the frequency counter, or turn off crystal trimming in the settings file.");    
                category = "yellow";
            }
            UpdateOutputText(ex.Message);
            UpdateColorDisplay(category);
            if (category == "yellow")
                AppendToOutputText("\n\nPlease unplug and re-plug USB and try test again.");
            else if (category == "red")
                AppendToOutputText("\n" +  "\n" +  "USB fails; please remove and discard.");
            else if (category == "generic")
            {
                if (ex.Message.Contains("NationalInstruments")) UpdateOutputText("Trimming error. Most likely cause is that this computer does not have the National Instruments GPIB software necessary to communicate with the frequency counter.  Disable trimming in the Settings.txt file to avoid this error.");
                AppendToOutputText("\n\nPlease unplug and re-plug USB and try test again.");
            }
            ErrorLogger(ex.Message);
        }

        public void Setup()
        {
            RunButtonEnabled(0);
            parameters.StartTime = new DateTime(2010, 1, 18);
            parameters.StartTime = DateTime.Now;
            parameters.MachineName = System.Environment.MachineName.Replace(' ', '_');
            //parameters.SetSerialNumber(); 
        }
        public void CheckRequirements()
        {
            uint deviceCount = thisUSB.CountDevices();
            if (deviceCount == 0) throw new Exception_Yellow("No devices attached.");
            if (deviceCount == 1 && parameters.testing) throw new Exception_Yellow("Either reference board or USB under test is missing.");
            if (referenceRadio == null && parameters.testing) throw new Exception_Yellow("Reference radio is not responding; please re-start test program.");
            if (parameters.testing && !referenceRadio.isOpen())
                referenceRadio.OpenPort(parameters.reference_radio_pid, 3000);
            if (parameters.crystaltrimming && !parameters.testing) throw new Exception("Error in settings file: if crystal trimming is enabled, testing must also be enabled.");
        }
        public void CheckConnections(ref bool counterattached, ref bool refradioattached)
        {
            if (parameters.crystaltrimming)
            {
                try
                {
                    Trimmer.testtrimmerisattached(parameters.counter_id);//add by nino
                }
                catch (Exception exc)
                {
                    if (exc.Message.Contains("GPIB") || exc.Message.Contains("handle is out of range")) exc = new Exception_Yellow("Error involving the frequency counter or GPIB connection (" + exc.Message + " 1).  Confirm that the GPIB cable is plugged into both the computer and the frequency counter, or turn off crystal trimming in the settings file.");
                    UpdateOutputText(exc.Message);
                    counterattached = false;
                }
            }
            if (parameters.testing)
            {
                try
                {
                    referenceRadio = new FTDIdevice(parameters, this);
                    referenceRadio.OpenPort(parameters.reference_radio_pid, 3000);
                    UpdateProgressBar_Detail(0);
                }
                catch (Exception exc)
                {
                    UpdateOutputText(exc.Message);
                    refradioattached = false;
                }
            }
        }
        public void ReadCounterID()
        {
//            if (CounterID.Text.Length != null && CounterID.Text.Length <= 2 )
            if(parameters.counter_id != null )
            {
                UpdateOutputText("Set counter ID.");
                System.Threading.Thread.Sleep(50);
            }
            else
            {
                throw new Exception_Red("error: Please enter correct Counter ID !! ");
            }
            //counterid = Convert.ToByte(CounterID.Text.ToUpper());
        }
        public void ReadBarcode()
        {
            //parameters.MAC = "804F580000000000";

            //if (ScannerInputBox.Text.Length < 16)
            if (ScannerInputBox.Text.Length < 41)
            {
                UpdateOutputText("Please Scan Barcode.");
                SelectScannerInputBox("");
            }

            string Mac = null;
            string sn = null;
            string temp_mac = null;
            string CandidateMac = null;
            int x = 1200;
            while (x-- > 0)
            {
                DoEvents("");
                CandidateMac = ScannerInputBox.Text.ToUpper();  // Candidate mac address is whatever's in the text box (but change it to uppercase for consistency)

                if (CandidateMac.Length == 41)
                {
                    if (CandidateMac.StartsWith(Parameters.MACheader))
                    {
                        sn = CandidateMac.Substring(7, 8);
                        Mac = CandidateMac.Substring(16,16);
                        temp_mac = CandidateMac.Substring(28,4);
                        break;
                    }                    
                    else throw new Exception_Yellow("Error: MAC address entered does not begin with the Liteon MAC header (0x80 0x4F 0x58).  Be careful not to type with the keyboard while using the barcode scanner.");
                }
                else
                {
                    //if (CandidateMac.Length > 16) UpdateOutputText("More than 16 digits have been entered -- this MAC address cannot be correct.  Please erase and re-scan or re-type.");
                    if (CandidateMac.Length > 41) UpdateOutputText("More than 41 digits have been entered -- this MAC address cannot be correct.  Please erase and re-scan or re-type.");
                    System.Threading.Thread.Sleep(50);
                }
            }
            if (x <= 0) throw new Exception_Yellow("Timed out after waiting 1 minute for barcode to scan.  Try again.");
            parameters.MAC = Mac;
            parameters.SN = sn;
            parameters.Temp_mac = temp_mac;//for simple OQC test 
            UpdateOutputText("Barcode accepted.");
            UpdateProgressBar_Overall(5);
        }
        private void EraseDongleHW3SW2firmware()
        {
            UpdateOutputText("erasing standard HW3_SW2 firmware from dongle...");
            thisUSB.adjustBaudRate(57600);
            byte[] x = { Convert.ToByte('<'), Convert.ToByte('0'), Convert.ToByte('F'), Convert.ToByte('1'), Convert.ToByte('2'), Convert.ToByte('4'), Convert.ToByte('8'), Convert.ToByte('A'), Convert.ToByte('A'), Convert.ToByte('B'), Convert.ToByte('B'), Convert.ToByte('>') };
            thisUSB.WriteByte(x);
            System.Threading.Thread.Sleep(2000);
            thisUSB.adjustBaudRate(Parameters.BaudRate_UARTandTesting);
        }
        public void TestUSB()
        {
            UpdateProgressBar_Overall(32);
            UpdateProgressBar_Detail(0);
            UpdateOutputText("Resetting chip...");
            thisUSB.ChipReset();
            //thisUSB.resetporttemp();  for experimenting with putting usb into suspend mode for faster chipreset

            LoadTestingFirmware();
            //TrimCrystal();
            RadioTest();
            //Utils.SendZTCCommand(thisUSB, "95 0A 02 " + Trimmer.TrimPacketPrep(4) + Trimmer.TrimPacketPrep(4));
            //Utils.SendZTCCommand(thisUSB, "95 0A 02 " + Trimmer.TrimPacketPrep(24) + Trimmer.TrimPacketPrep(11));
        }
        private void LoadTestingFirmware()
        {
            thisUSB.adjustBaudRate(Parameters.BaudRate_UARTandTesting);
            //thisUSB.adjustBaudRate(Parameters.loadingBaudRate);  //increase the baud rate 8x, saves about 2 seconds on loading 
            UpdateOutputText("Loading testing firmware...");
            RAMLoader loadZTC = new RAMLoader(parameters, thisUSB, parameters.ZTCfilepath, this);
            loadZTC.Run(false);
            UpdateProgressBar_Overall(38);
            System.Threading.Thread.Sleep(10);                  //for some reason this is necessary or else radio test fails, don't know why :-/
            thisUSB.adjustBaudRate(Parameters.BaudRate_UARTandTesting);  //return to baud rate that the ZTC is written for for testing.  consider speeding ZTC up later and testing to see if it affects quality/speed.
        }
        private void SetCrystalTrim()
        {            
            Trimmer trimmer = new Trimmer(parameters, thisUSB);//init frequency counter @20120328 by nino
            //pick our trim values, either by calculating them based on repeated frequency measurements and newton's method, or by looking them up in a database, or by choosing the default ones that are in the settings file.
            if (parameters.crystaltrimming)
            {
                System.Threading.Thread.Sleep(50);  //added in because first ZTC packet in trimming wasn't getting a response, prob bc ZTC was still booting
                UpdateOutputText("Calculating optimal crystal trim values...");
                //Trimmer trimmer = new Trimmer(parameters, thisUSB);
                trimmer.Run();                      //Communicates with the frequency counter via GPIB connection to take frequency measurements and adjust the trim values until frequency is close to 12 MHz.
            }
            else if (parameters.lookinguptrimsbool)
            {
                LookupTrimValues();
            }
            else if (parameters.settingdefaulttrimsbool)
            {                
                SetDefaultTrimValues();
                trimmer.Feedback_freq();//to get frequency counter data @20120328 by nino
            }
            //if set to measure the freqency once (presumably after setting looked-up or default trims), do so.
            if (parameters.checkingfreqafternottrimmingbool)
            {
                //Trimmer trimmer = new Trimmer(parameters, thisUSB);
                trimmer.TakeSingleMeasurement();
            }
            UpdateProgressBar_Overall(47);
        }
        private void RadioTest()
        {
            //FTDIdevice referenceRadio = new FTDIdevice(parameters);5
            //referenceRadio.OpenPort(parameters.reference_radio_pid);  //would it be possible to do this once and leave it open between tests?
            //referenceRadio.Close();
            //referenceRadio.OpenPort(parameters.reference_radio_pid);  
            //try
            //{
            UpdateOutputText("Testing radio communication quality...");
            RadioTester radiotest = new RadioTester(parameters, thisUSB, referenceRadio);
            parameters.radioTestsuccesses = radiotest.RunRadioTest(parameters.numberofradiotestsEachWay);
            parameters.radiotestsuccesspercent = 100 * parameters.radioTestsuccesses / (2 * parameters.numberofradiotestsEachWay);  //do i need to cast these as doubles?
            parameters.radiotestsuccesspercentstring = parameters.radiotestsuccesspercent.ToString();
            //referenceRadio.Close();  //possible to only do this on formclosing?  see comment above at opening

            UpdateProgressBar_Overall(55);
            //}
            //catch (Exception ex)
            //{
            //    referenceRadio.Close();  //If an error is thrown, we want to close the reference radio so we can reopen it next time.
            //    throw ex;
            //}
        }
        public void LoadFirmware()
        {
            UpdateOutputText("Resetting chip...");
            thisUSB.ChipReset();
            UpdateOutputText("Loading secondary stage loader...");
            FirmwareLoader loadFW = new FirmwareLoader(parameters, thisUSB, this);//, parameters.FWimagefilepath);
            loadFW.Run();
            //loadFW.LoadHardwareSettings();
            UpdateProgressBar_Overall(95);
        }
        public void Finish()
        {
            parameters.TimeStamp = new DateTime(2010, 1, 18);
            parameters.TimeStamp = DateTime.Now;
            System.TimeSpan diff = parameters.TimeStamp.Subtract(parameters.StartTime);
            double testtime = diff.TotalSeconds;
            if (testtime < 86400) parameters.totaltesttime = (Math.Round(testtime, 3)).ToString();  //don't record the test time if it doesn't make sense (which would happen if we hit an error before recording the StartTime)
            UpdateProgressBar_Overall(progressBar_overall.Maximum);
            UpdateProgressBar_Detail(100);
            SelectScannerInputBox("");
            RunButtonEnabled(1);
        }
        public void Logger()
        {
            UpdateOutputText("Saving test results to log...");

            //LOGGING
            using (StreamWriter writer = new StreamWriter(Parameters.logfilepath, true))
            {
                writer.WriteLine(parameters.logfilestring());
                //writer.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\tc\t{11}\tf\t{12}\t{13}\t{14}", 
                //    parameters.TimeStamp.Date, parameters.TimeStamp.TimeOfDay, parameters.MAC, VID, PID, parameters.FTDISerialNum, parameters.testingandorloading, 
                //    parameters.totaltesttime, parameters.MachineName, parameters.FWimagefilename, parameters.radio, FTintTestingBoard.GetFinalCoarse(), FTintTestingBoard.GetFinalFine(), FinalFreq, TrimSeq);
                ////fw filename, version number of this program, radiotest success % and number of tests, 
            }
            using (StreamWriter writer = new StreamWriter(Parameters.backuplogfilepath, true))
            {
                writer.WriteLine(parameters.logfilestring());
            }
        }
        public void ErrorLogger(string exceptionmessage)
        {
            using (StreamWriter writer = new StreamWriter(Parameters.errorlogfilepath, true))
            {
                writer.WriteLine(parameters.logfilestring()+'\t'+exceptionmessage.Replace(' ','_'));
            }
        }

        public void enableResetLine()
        {
                UpdateProgressBar_Overall(5);
                if (!thisUSB.resetLineIsEnabled_pluschecks())  //reads eeprom and checks if CBUS3 reset line needs to be enabled or not, also checks prior testing info - status and mac
                {
                    UpdateOutputText("Enabling chip reset line...");
                    thisUSB.enableResetLine();
                        UpdateProgressBar_Overall(8);
                        UpdateProgressBar_Detail(20);
                    thisUSB.PortCycle();
                        UpdateProgressBar_Overall(14);
                        UpdateProgressBar_Detail(50);
                    System.Threading.Thread.Sleep(500);  //necessary?
                        UpdateProgressBar_Overall(19);
                        UpdateProgressBar_Detail(100);
                    thisUSB.OpenPort(parameters.USB_under_test_pid, 8000);
                        UpdateProgressBar_Overall(30);
                }
                else
                { }
        }
        private string openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            string filepathFromDialog = "";
            Stream myStream = null;
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            openFileDialog1.InitialDirectory = Parameters.loadingfilepath;
            openFileDialog1.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 2;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if ((myStream = openFileDialog1.OpenFile()) != null)
                    {
                        using (myStream)
                        {
                            filepathFromDialog = openFileDialog1.FileName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                }
            }
            return filepathFromDialog;
        }
        public void LookupTrimValues()
        {
            StreamReader SR;
            string S;
            SR = File.OpenText(parameters.trimsdatabasefilepath);
            S = SR.ReadToEnd();
            int coarseindex, fineindex;
            try
            {
                int thisMACindex = S.IndexOf(parameters.MAC);
                coarseindex = S.IndexOf("c", thisMACindex, 120) + 2;  //within the next 145 characters, look for "c" and use it to find coarse trim
                fineindex = S.IndexOf("f", thisMACindex, 120) + 2;      //these will throw some kind of count error if we're too close to the end of the file.  "Count must be positive and count must refer to a location within the string/array/collection."
            }
            catch (System.ArgumentOutOfRangeException exce)
            {
                throw new Exception_Yellow("No record of this MAC address was found in the trim database file (or this is a short final record).");
            }
            //fix the following once log file format is finalized. ****************
            parameters.coarsetrim_SET = Convert.ToInt16(S.Substring(coarseindex, 2));
            parameters.finetrim_SET = Convert.ToInt16(S.Substring(fineindex, 2));
        }
        public void SetDefaultTrimValues()
        {
            parameters.coarsetrim_SET = parameters.coarsetrim_default;
            parameters.finetrim_SET = parameters.finetrim_default;
        }
        public void SaveSerialNumber()
        {
            uint nextSNafterthisone = parameters.serialnumberlower3bytes + 1;
            using (StreamWriter s = new StreamWriter(Parameters.nextSNfilepath, false)) //overwrite mode
            {
                s.WriteLine(nextSNafterthisone.ToString());
            }
        }

        //functions updating window appearance:
        public void UpdateOutputText(string value)
        {
            if (InvokeRequired)
            {
                // We're not in the UI thread, so we need to call BeginInvoke
                BeginInvoke(new StringParameterDelegate(UpdateOutputText), new object[] { value });
                return;
            }
            // Must be on the UI thread if we've got this far
            Output.Text = value;
            System.Windows.Forms.Application.DoEvents();
        }
        public void AppendToOutputText(string value)
        {
            if (InvokeRequired)
            {
                // We're not in the UI thread, so we need to call BeginInvoke
                BeginInvoke(new StringParameterDelegate(AppendToOutputText), new object[] { value });
                return;
            }
            // Must be on the UI thread if we've got this far
            Output.Text += value;
            System.Windows.Forms.Application.DoEvents();
        }
        public void DoEvents(string value)  //just for keeping window alive and closeable
        {
            if (InvokeRequired)
            {
                // We're not in the UI thread, so we need to call BeginInvoke
                BeginInvoke(new StringParameterDelegate(DoEvents), new object[] { value });
                return;
            }
            // Must be on the UI thread if we've got this far
            System.Windows.Forms.Application.DoEvents();
        }
        //private void ReadSettingsFile()
        //{
        //    StreamReader SR;
        //    string S;
        //    SR = File.OpenText(Parameters.settingsfilepath);
        //    while (true)
        //    {
        //        S = SR.ReadLine();
        //        if (S.Contains("END SETTINGS FILE")) break;
                

        //        switch (S)
        //        {
        //            case Parameters.SettingsString_testingbool:
        //                S = SR.ReadLine();
        //                if (S != "" && !S.StartsWith("//"))
        //                {
        //                    if (S == "true") parameters.testing = true;
        //                    else if (S == "false") parameters.testing = false;
        //                    else throw new Exception("The \"" + Parameters.SettingsString_testingbool + "\" line in settings file must say either true or false, no spaces.");
        //                }
        //                break;
        //            case Parameters.SettingsString_loadingbool:
        //                S = SR.ReadLine();
        //                if (S != "" && !S.StartsWith("//"))
        //                {
        //                    if (S == "true") parameters.loading = true;
        //                    else if (S == "false") parameters.loading = false;
        //                    else throw new Exception("The \"" + Parameters.SettingsString_loadingbool + "\" line in settings file must say either true or false, no spaces.");
        //                }
        //                break;
        //            case Parameters.SettingsString_USBUnderTestPID:
        //                S = SR.ReadLine();
        //                if (S != "" && !S.StartsWith("//")) parameters.USB_under_test_pid = Convert.ToUInt32(S,16);//Convert.ToUInt32(String.Format("{0:X}", Convert.ToUInt32(S)));
        //                break;
        //            case Parameters.SettingsString_RefRadioPID:
        //                S = SR.ReadLine();
        //                if (S != "" && !S.StartsWith("//")) parameters.reference_radio_pid= Convert.ToUInt32(S,16);
        //                break;
        //            case Parameters.SettingsString_finalPID:
        //                S = SR.ReadLine();
        //                if (S != "" && !S.StartsWith("//")) parameters.finalPID = Convert.ToUInt32(S,16);
        //                break;
        //            case Parameters.SettingsString_PIDtoavoidopening:
        //                S = SR.ReadLine();
        //                if (S != "" && !S.StartsWith("//")) parameters.PIDtoavoidopening = Convert.ToUInt32(S, 16);
        //                break;
        //            case Parameters.SettingsString_trimmingbool:
        //                S = SR.ReadLine();
        //                if (S != "" && !S.StartsWith("//"))
        //                {
        //                    if (S == "true") parameters.crystaltrimming = true;
        //                    else if (S == "false") parameters.crystaltrimming = false;
        //                    else throw new Exception("The \"" + Parameters.SettingsString_trimmingbool + "\" line in settings file must say either true or false, no spaces.");
        //                }
        //                break;
        //            case Parameters.SettingsString_numberofradiotestsEachWay:
        //                S = SR.ReadLine();
        //                if (S != "" && !S.StartsWith("//")) parameters.numberofradiotestsEachWay = Convert.ToInt32(S);
        //                break;
        //            case Parameters.SettingsString_ZTCfilename:
        //                S = SR.ReadLine();
        //                if (S != "" && !S.StartsWith("//")) parameters.ZTCfilename = S;
        //                break;
        //            case Parameters.SettingsString_SSLfilename:
        //                S = SR.ReadLine();
        //                if (S != "" && !S.StartsWith("//")) parameters.SSLfilename = S;
        //                break;
        //            case Parameters.SettingsString_FWimagefilename:
        //                S = SR.ReadLine();
        //                if (S != "" && !S.StartsWith("//")) parameters.FWimagefilename = S;
        //                break;
        //            case Parameters.SettingsString_takingFWfilenamefromsettingsfile:
        //                S = SR.ReadLine();
        //                if (S != "" && !S.StartsWith("//"))
        //                {
        //                    if (S == "true") parameters.takingFWfilenamefromsettingsfile = true;
        //                    else if (S == "false") parameters.takingFWfilenamefromsettingsfile = false;
        //                    else throw new Exception("The \"" + Parameters.SettingsString_takingFWfilenamefromsettingsfile + "\" line in settings file must say either true or false, no spaces.");
        //                }
        //                break;
        //        }
        //    }
        //    parameters.ZTCfilepath = Parameters.testingfilepath + "\\" + parameters.ZTCfilename;
        //    parameters.SSLfilepath = Parameters.loadingfilepath + "\\" + parameters.SSLfilename;
        //    parameters.FWimagefilepath = Parameters.loadingfilepath + "\\" + parameters.FWimagefilename;

        //    if (parameters.testing && parameters.loading) parameters.testingandorloading = "Testing+Loading";
        //    else if (parameters.testing) parameters.testingandorloading = "Testing_Only";
        //    else if (parameters.loading) parameters.testingandorloading = "Loading_Only";
        //    else throw new Exception("Program is set to neither test nor load this USB.  Change options in Settings.txt file.");
        //}
        public void SelectScannerInputBox(string value)
        {
            if (InvokeRequired)
            {
                // We're not in the UI thread, so we need to call BeginInvoke
                BeginInvoke(new StringParameterDelegate(SelectScannerInputBox), new object[] { value });
                return;
            }
            // Must be on the UI thread if we've got this far
            ScannerInputBox.Select();
            ScannerInputBox.Text = "";
            System.Windows.Forms.Application.DoEvents();
        }
        public void UpdateColorDisplay(string color)
        {
            if (InvokeRequired)
            {
                // We're not in the UI thread, so we need to call BeginInvoke
                BeginInvoke(new StringParameterDelegate(UpdateColorDisplay), new object[] { color });
                return;
            }
            // Must be on the UI thread if we've got this far
            if (color == "white") StatusIndicator.BackColor = System.Drawing.Color.White;
            else if (color == "red") StatusIndicator.BackColor = System.Drawing.Color.Red;
            else if (color == "yellow") StatusIndicator.BackColor = System.Drawing.Color.Yellow;
            else if (color == "green") StatusIndicator.BackColor = System.Drawing.Color.LimeGreen;
            else if (color == "orange") StatusIndicator.BackColor = System.Drawing.Color.Orange;
            else StatusIndicator.BackColor = System.Drawing.Color.MediumOrchid;
            System.Windows.Forms.Application.DoEvents();
        }
        public void UpdateProgressBar_Detail(int progress)
        {
            if (InvokeRequired)
            {
                // We're not in the UI thread, so we need to call BeginInvoke
                BeginInvoke(new IntParameterDelegate(UpdateProgressBar_Detail), new object[] { progress });
                return;
            }
            // Must be on the UI thread if we've got this far
            progressBar_detail.Value = progress;
            System.Windows.Forms.Application.DoEvents();
        }
        void UpdateProgressBar_Overall(int progress)
        {
            if (InvokeRequired)
            {
                // We're not in the UI thread, so we need to call BeginInvoke
                BeginInvoke(new IntParameterDelegate(UpdateProgressBar_Overall), new object[] { progress });
                return;
            }
            // Must be on the UI thread if we've got this far
            progressBar_overall.Value = progress;
            System.Windows.Forms.Application.DoEvents();
        }
        void RunButtonEnabled(int enabled)
        {
            if (InvokeRequired)
            {
                // We're not in the UI thread, so we need to call BeginInvoke
                BeginInvoke(new IntParameterDelegate(RunButtonEnabled), new object[] { enabled });
                return;
            }
            // Must be on the UI thread if we've got this far
            if (enabled == 1) RunButton.Enabled = true;
            else RunButton.Enabled = false;
            System.Windows.Forms.Application.DoEvents();
        }


        //Barcode scanning from old program:
        //string ScanBarcode()
        //{
        //    string settingsfilepath = directorystring + "Settings.txt";
        //    StreamReader SR = File.OpenText(@settingsfilepath);
        //    string s = SR.ReadToEnd();
        //    int rangebottomindex = s.IndexOf("MAC range from 0x804F58") + 23;
        //    int rangetopindex = s.IndexOf("to 0x804F58") + 11;
        //    Int64 rangebottom = Int64.Parse(s.Substring(rangebottomindex, 10), System.Globalization.NumberStyles.HexNumber);
        //    Int64 rangetop = Int64.Parse(s.Substring(rangetopindex, 10), System.Globalization.NumberStyles.HexNumber);

        //    if (textBox1.Text.Length < 16)
        //    {
        //        Output.Text = "Please Scan Barcode.";
        //        textBox1.Select();
        //        System.Windows.Forms.Application.DoEvents();
        //    }

        //    int x = 0;
        //    string Mac = null;
        //    string CandidateMac = null;

        //    while (x < 400)
        //    {
        //        System.Windows.Forms.Application.DoEvents();
        //        CandidateMac = textBox1.Text.ToUpper();  // Candidate mac address is whatever's in the text box (but change it to uppercase for consistency)

        //        if (CandidateMac.Length == 16)  // if 16 digits have been entered
        //        {
        //            x = 10000;           // break out of loop next time around
        //            if (CandidateMac.StartsWith(MacHeader))  // and if the first 3 bytes are our MAC header
        //            {
        //                Int64 num = Int64.Parse(CandidateMac.Substring(6, 10), System.Globalization.NumberStyles.HexNumber);
        //                if (num >= rangebottom && num <= rangetop)
        //                {
        //                    StreamReader testTxt = new StreamReader(@logfilepath);
        //                    string allRead = testTxt.ReadToEnd();            //Reads the whole text file to the end
        //                    testTxt.Close();                                 //Closes the text file after it is fully read.

        //                    if (!Regex.IsMatch(allRead, CandidateMac))        // and if this MAC address hasn't already been assigned
        //                    {
        //                        Mac = CandidateMac;                            // then make the 16 digits our MAC address
        //                    }
        //                    else throw new Exception("Error: MAC address entered has already been assigned.");
        //                }
        //                else throw new Exception("Error: MAC address does not fall within designated range.  ");
        //            }
        //            else throw new Exception("Error: MAC address entered does not begin with the ThinkEco MAC header (0x80 0x4F 0x58).  Be careful not to type with the keyboard while using the barcode scanner.");
        //        }
        //        else
        //        {
        //            x++;
        //            System.Threading.Thread.Sleep(100);
        //        }
        //    }
        //    if (x == 400) throw new Exception("Test timed out.  Don't forget to scan barcode!");

        //    System.Windows.Forms.Application.DoEvents();
        //    return Mac;
        //}

    }
}
