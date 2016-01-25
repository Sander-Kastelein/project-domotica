// Xamarin/C# app voor de besturing van een Arduino (Uno with Ethernet Shield) m.b.v. een socket-interface.
// Arduino server: DomoticaServer.ino
// De besturing heeft betrekking op het aan- en uitschakelen van een Arduino pin, 
// waar een led aan kan hangen of, t.b.v. het Domotica project, een RF-zender waarmee een 
// klik-aan-klik-uit apparaat bestuurd kan worden.
//
// De app heeft twee modes die betrekking hebben op de afhandeling van de socket-communicatie: "simple-mode" en "threaded-mode" 
// Wanneer het statement    //connector = new Connector(this);    wordt uitgecommentarieerd draait de app in "simple-mode",
// Het opvragen van gegevens van de Arduino (server) wordt dan met een Timer gerealisseerd. (De extra classes Connector.cs, 
// Receiver.cs en Sender.cs worden dan niet gebruikt.) 
// Als er een connector wordt aangemaakt draait de app in "threaded mode". De socket-communicatie wordt dan afgehandeld
// via een Sender- en een Receiver klasse, die worden aangemaakt in de Connector klasse. Deze threaded mode 
// biedt een generiekere en ook robuustere manier van communicatie, maar is ook moeilijker te begrijpen. 
// Aanbeveling: start in ieder geval met de simple-mode
//
// Werking: De communicatie met de (Arduino) server is gebaseerd op een socket-interface. Het IP- en Port-nummer
// is instelbaar. Na verbinding kunnen, middels een eenvoudig commando-protocol, opdrachten gegeven worden aan 
// de server (bijv. pin aan/uit). Indien de server om een response wordt gevraagd (bijv. led-status of een
// sensorwaarde), wordt deze in een 4-bytes ASCII-buffer ontvangen, en op het scherm geplaatst. Alle commando's naar 
// de server zijn gecodeerd met 1 char. Bestudeer het protocol in samenhang met de code van de Arduino server.
// Het default IP- en Port-nummer (zoals dat in het GUI verschijnt) kan aangepast worden in de file "Strings.xml". De
// ingestelde waarde is gebaseerd op je eigen netwerkomgeving, hier, en in de Arduino-code, is dat een router, die via DHCP
// in het segment 192.168.1.x vanaf systeemnummer 100 IP-adressen uitgeeft.
// 
// Resource files:
//   Main.axml (voor het grafisch design, in de map Resources->layout)
//   Strings.xml (voor alle statische strings in het interface, in de map Resources->values)
// 
// De software is verder gedocumenteerd in de code. Tijdens de colleges wordt er nadere uitleg over gegeven.
// 
// Versie 1.0, 12/12/2015
// D. Bruin
// S. Oosterhaven
// W. Dalof (voor de basis van het Threaded interface)
//
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Content.PM;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Android.Graphics;
using System.Threading.Tasks;

namespace Domotica
{
    [Activity(Label = "@string/application_name", MainLauncher = true, Icon = "@drawable/Icon3", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]

    public class MainActivity : Activity
    {
        // Variables (components/controls)
        // Controls on GUI
        Button buttonConnect, ChangeRFState, ChangeRFState2, ChangeRFState3, ToggleCMode, ToggleCMode2;
        Button buttonChangePinState;
        TextView textViewServerConnect, textViewTimerStateValue, RFConnect, RFConnect2, RFConnect3;
        public TextView textViewChangePinStateValue, textViewSensorValue, textViewSensorValue2, textViewRFConnectValue;
        EditText editTextIPAddress, editTextIPPort, EditSetTimer;

        Timer timerClock, timerSockets;             // Timers   
        Socket socket = null;                       // Socket   
        Connector connector = null;                 // Connector (simple-mode or threaded-mode)
        List<Tuple<string, TextView>> commandList = new List<Tuple<string, TextView>>();  // List for commands and response places on UI
        int listIndex = 0;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource (strings are loaded from Recources -> values -> Strings.xml)
            SetContentView(Resource.Layout.Main);

            // find and set the controls, so it can be used in the code
            ChangeRFState = FindViewById<Button>(Resource.Id.buttonChangeRFState);
            ChangeRFState2 = FindViewById<Button>(Resource.Id.buttonChangeRFState2);
            ChangeRFState3 = FindViewById<Button>(Resource.Id.buttonChangeRFState3);
            ToggleCMode = FindViewById<Button>(Resource.Id.ToggleCMode);
            ToggleCMode2 = FindViewById<Button>(Resource.Id.ToggleCMode2);
            buttonConnect = FindViewById<Button>(Resource.Id.buttonConnect);
            buttonChangePinState = FindViewById<Button>(Resource.Id.buttonChangePinState);
            RFConnect = FindViewById<TextView>(Resource.Id.RFConnect);
            RFConnect2 = FindViewById<TextView>(Resource.Id.RFConnect2);
            RFConnect3 = FindViewById<TextView>(Resource.Id.RFConnect3);
            textViewTimerStateValue = FindViewById<TextView>(Resource.Id.textViewTimerStateValue);
            textViewServerConnect = FindViewById<TextView>(Resource.Id.textViewServerConnect);
            textViewChangePinStateValue = FindViewById<TextView>(Resource.Id.textViewChangePinStateValue);
            textViewSensorValue = FindViewById<TextView>(Resource.Id.textViewSensorValue);
            textViewSensorValue2 = FindViewById<TextView>(Resource.Id.textViewSensorValue2);
            textViewRFConnectValue = FindViewById<TextView>(Resource.Id.RFConnect);
            editTextIPAddress = FindViewById<EditText>(Resource.Id.editTextIPAddress);
            editTextIPPort = FindViewById<EditText>(Resource.Id.editTextIPPort);
            EditSetTimer = FindViewById<EditText>(Resource.Id.EditSetTimer);

            UpdateConnectionState(4, "Disconnected");

            // Init commandlist, scheduled by socket timer
            commandList.Add(new Tuple<string, TextView>("s", textViewChangePinStateValue));
            commandList.Add(new Tuple<string, TextView>("a", textViewSensorValue));
            commandList.Add(new Tuple<string, TextView>("b", textViewSensorValue2));

            // activation of connector -> threaded sockets otherwise -> simple sockets 
            // connector = new Connector(this);

            this.Title = (connector == null) ? this.Title + " (simple sockets)" : this.Title + " (thread sockets)";

            // timer object, running clock
            timerClock = new System.Timers.Timer() { Interval = 1000, Enabled = true }; // Interval >= 100
            timerClock.Elapsed += (obj, args) =>
            {
                    RunOnUiThread(() =>
                    {
                        textViewTimerStateValue.Text = DateTime.Now.ToString("hh:mm:ss");
                    });

                    string InsertTimerValue = Convert.ToString(EditSetTimer.Text).Replace(".", ":");
                    if (DateTime.Now.ToString("hh:mm:ss") == InsertTimerValue)
                    {
                        executeCommand("1");
                        executeCommand("2");
                        executeCommand("3");
                    }
            };

            // timer object, check Arduino state
            // Only one command can be serviced in an timer tick, schedule from list
            timerSockets = new System.Timers.Timer() { Interval = 150, Enabled = false }; // Interval = 50
            timerSockets.Elapsed += (obj, args) =>
            {
                RunOnUiThread(() =>
                {
                if (socket != null) // only if socket exists
                {
                    // Send a command to the Arduino server on every tick (loop though list)
                    UpdateGUI(executeCommand(commandList[listIndex].Item1), commandList[listIndex].Item2);  //e.g. UpdateGUI(executeCommand("s"), textViewChangePinStateValue);
                    if (++listIndex >= commandList.Count) listIndex = 0;
                }
                else timerSockets.Enabled = false;  // If socket broken -> disable timer
                });
            };

            // RF1 aan/uit
            if (ChangeRFState != null)  // if button exists
            {

                ChangeRFState.Click += (sender, e) =>
                {
                    string RFConText = "Disconnected";  // default text
                    Color color = Color.Red;
                    string state = executeCommand("1");
                    if (state == "AAN")
                    {
                        // Switch 1 staat aan.
                        RFConText = "Connected";  // default text
                        color = Color.Green;
                    }
                    else if (state == "UIT")
                    {
                        // Switch 1 staat uit.
                        RFConText = "Disconnected";
                        color = Color.Red;
                    }
                    RunOnUiThread(() =>
                       {
                           if (RFConText != null)  // text exists
                           {
                               RFConnect.Text = RFConText;
                               RFConnect.SetTextColor(color);
                           }
                       });
                };

                // RF 2 aan/uit
                if (ChangeRFState2 != null) // if button exists
                {
                    ChangeRFState2.Click += (sender, e) =>
                    {
                        string RFConText2 = "Disconnected";  // default text
                        Color color = Color.Red;
                        string state2 = executeCommand("2");
                        if (state2 == "AAN")
                        {
                            // Switch 1 staat aan.
                            RFConText2 = "Connected";  // default text
                            color = Color.Green;
                        }
                        else if (state2 == "UIT")
                        {
                            // Switch 1 staat uit.
                            RFConText2 = "Disconnected";
                            color = Color.Red;
                        }
                        RunOnUiThread(() =>
                        {
                            if (RFConText2 != null)  // text exists
                            {
                                RFConnect2.Text = RFConText2;
                                RFConnect2.SetTextColor(color);
                            }
                        });
                    };

                    // RF3 aan/uit
                    if (ChangeRFState3 != null) // if button exists
                    {
                        ChangeRFState3.Click += (sender, e) =>
                        {
                            string RFConText3 = "Disconnected";  // default text
                            Color color = Color.Red;                // default color
                            string state3 = executeCommand("3");     // state is send string an received string
                            if (state3 == "AAN")                     // is the received string == "Aan"
                            {
                                // Switch 3 staat aan.
                                RFConText3 = "Connected";                // default text when rf3 is on
                                color = Color.Green;                    // make the color green
                            }
                            else if (state3 == "UIT")
                            {
                                // Switch 3 staat uit.
                                RFConText3 = "Disconnected";             // Default text when rf3 is off
                                color = Color.Red;                  // Make the color green
                            }
                            RunOnUiThread(() =>                     // Run this on the User Interface thread (functie)
                            {
                                if (RFConText3 != null)              // text exists
                                {
                                    RFConnect3.Text = RFConText3;    // replace the current text with the new text from if statement "AAN" || "UIT"
                                    RFConnect3.SetTextColor(color); // change the color of the new text in the color of the if statement "AAN" || "UIT"
                                }
                            });
                        };
                        // C-MODE Normaal aan (Opdracht c)
                        if (ToggleCMode != null) // if button exists
                        {
                            ToggleCMode.Click += (sender, e) =>
                            {
                                string state4 = executeCommand("c");
                            };

                            // C-MODE First aan (Opdracht c)
                            if (ToggleCMode2 != null) // if button exists
                            {
                                ToggleCMode2.Click += (sender, e) =>
                                {
                                    string state5 = executeCommand("C");
                                };
                                //Add the "Connect" button handler.
                                if (buttonConnect != null)  // if button exists
                                {
                                    buttonConnect.Click += (sender, e) =>
                                    {
                                    //Validate the user input (IP address and port)
                                    if (CheckValidIpAddress(editTextIPAddress.Text) && CheckValidPort(editTextIPPort.Text))
                                        {
                                            if (connector == null) // -> simple sockets
                                        {
                                                ConnectSocket(editTextIPAddress.Text, editTextIPPort.Text);
                                            }
                                            else // -> threaded sockets
                                        {
                                            //Stop the thread If the Connector thread is already started.
                                            if (connector.CheckStarted()) connector.StopConnector();
                                                connector.StartConnector(editTextIPAddress.Text, editTextIPPort.Text);
                                            }
                                        }
                                        else UpdateConnectionState(3, "Please check IP");
                                    };
                                }

                                //Add the "Change pin state" button handler.
                                if (buttonChangePinState != null)
                                {
                                    buttonChangePinState.Click += (sender, e) =>
                                    {
                                        if (connector == null) // -> simple sockets
                                        {
                                            executeCommand("t");                 // Send toggle-command to the Arduino
                                        }
                                    };
                                }
                            }
                        }
                    }
                }
            }
        }

        //Send command to server and wait for response (blocking)
        //Method should only be called when socket existst
        public string executeCommand(string cmd)
        {
            byte[] buffer = new byte[4]; // response is always 4 bytes
            int bytesRead = 0;
            string result = "---";

            if (socket != null)
            {
                //Send command to server
                socket.Send(Encoding.ASCII.GetBytes(cmd));

                try //Get response from server
                {
                    //Store received bytes (always 4 bytes, ends with \n)
                    bytesRead = socket.Receive(buffer);  // If no data is available for reading, the Receive method will block until data is available,
                    //Read available bytes.              // socket.Available gets the amount of data that has been received from the network and is available to be read
                    while (socket.Available > 0) bytesRead = socket.Receive(buffer);
                    if (bytesRead == 4)
                        result = Encoding.ASCII.GetString(buffer, 0, bytesRead - 1); // skip \n
                    else result = "err";
                }
                catch (Exception exception) {
                    result = exception.ToString();
                    if (socket != null) {
                        socket.Close();
                        socket = null;
                    }
                    UpdateConnectionState(3, result);
                }
            }
            return result;
        }

        //Update connection state label (GUI).
        public void UpdateConnectionState(int state, string text)
        {
            // connectButton
            string butConText = "Connect";  // default text
            bool butConEnabled = true;      // default state
            Color color = Color.Red;        // default color
            // pinButton
            bool butPinEnabled = false;     // default state 

            //Set "Connect" button label according to connection state.
            if (state == 1)
            {
                butConText = "Please wait";
                color = Color.Orange;
                butConEnabled = false;
            } else
            if (state == 2)
            {
                butConText = "Disconnect";
                color = Color.Green;
                butPinEnabled = true;
            }
            //Edit the control's properties on the UI thread
            RunOnUiThread(() =>
            {
                textViewServerConnect.Text = text;
                if (butConText != null)  // text existst
                {
                    buttonConnect.Text = butConText;
                    textViewServerConnect.SetTextColor(color);
                    buttonConnect.Enabled = butConEnabled;
                }
                buttonChangePinState.Enabled = butPinEnabled;
            });
        }

        //Update GUI based on Arduino response
        public void UpdateGUI(string result, TextView textview)
        {
            RunOnUiThread(() =>
            {
                if (result == "UIT")
                {
                    textview.SetTextColor(Color.Red);
                }
                else if (result == "AAN")
                {
                    textview.SetTextColor(Color.Green);
                }
                else textview.SetTextColor(Color.Green);
                textview.Text = result;
            });
        }

        // Connect to socket ip/prt (simple sockets)
        public void ConnectSocket(string ip, string prt)
        {
            RunOnUiThread(() =>
            {
                if (socket == null)                                       // create new socket
                {
                    UpdateConnectionState(1, "Connecting...");
                    try  // to connect to the server (Arduino).
                    {
                        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        socket.Connect(new IPEndPoint(IPAddress.Parse(ip), Convert.ToInt32(prt)));
                        if (socket.Connected)
                        {
                            UpdateConnectionState(2, "Connected");
                            timerSockets.Enabled = true;                //Activate timer for communication with Arduino     
                        }
                    } catch (Exception exception) {
                        timerSockets.Enabled = false;
                        if (socket != null)
                        {
                            socket.Close();
                            socket = null;
                        }
                        UpdateConnectionState(4, exception.Message);
                    }
	            }
                else // disconnect socket
                {
                    socket.Close(); socket = null;
                    timerSockets.Enabled = false;
                    UpdateConnectionState(4, "Disconnected");
                }
            });
        }

        //Close the connection (stop the threads) if the application stops.
        protected override void OnStop()
        {
            base.OnStop();

            if (connector != null)
            {
                if (connector.CheckStarted())
                {
                    connector.StopConnector();
                }
            }
        }

        //Close the connection (stop the threads) if the application is destroyed.
        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (connector != null)
            {
                if (connector.CheckStarted())
                {
                    connector.StopConnector();
                }
            }
        }

        //Prepare the Screen's standard options menu to be displayed.
        public override bool OnPrepareOptionsMenu(IMenu menu)
        {
            //Prevent menu items from being duplicated.
            menu.Clear();

            MenuInflater.Inflate(Resource.Menu.menu, menu);
            return base.OnPrepareOptionsMenu(menu);
        }

        //Executes an action when a menu button is pressed.
        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.exit:
                    //Force quit the application.
                    System.Environment.Exit(0);
                    return true;
                case Resource.Id.abort:

                    //Stop threads forcibly (for debugging only).
                    if (connector != null)
                    {
                        if (connector.CheckStarted()) connector.Abort();
                    }
                    return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        //Check if the entered IP address is valid.
        private bool CheckValidIpAddress(string ip)
        {
            if (ip != "") {
                //Check user input against regex (check if IP address is not empty).
                Regex regex = new Regex("\\b((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)(\\.|$)){4}\\b");
                Match match = regex.Match(ip);
                return match.Success;
            } else return false;
        }

        //Check if the entered port is valid.
        private bool CheckValidPort(string port)
        {
            //Check if a value is entered.
            if (port != "")
            {
                Regex regex = new Regex("[0-9]+");
                Match match = regex.Match(port);

                if (match.Success)
                {
                    int portAsInteger = Int32.Parse(port);
                    //Check if port is in range.
                    return ((portAsInteger >= 0) && (portAsInteger <= 65535));
                }
                else return false;
            } else return false;
        }

    }
}
