
#include <RemoteReceiver.h>
#include <RemoteTransmitter.h>
#include <RCSwitch.h>
#include <SPI.h>                  // Ethernet shield uses SPI-interface
#include <Ethernet.h>             // Ethernet library

// Preprocessor defines
#define RFPin                   4  // output, pin to control the RF-sender (and Click-On Click-Off-device)
#define ledPin                  8  // output, led used for "connect state": blinking = searching; continuously = connected
#define analogPinLight          0  // sensor value (lightsensor)
#define analogPinTemperature    1  // Sensor value (Temperature sensor)

#define ethPort      3300 // Network port
unsigned char mac[]   =    { 0x40, 0x6c, 0x8f, 0x36, 0x84, 0x8a };

// "Singletons"
RCSwitch mySwitch = RCSwitch();
EthernetServer server(ethPort);              // EthernetServer instance (listening on port <ethPort>).

/* Status van de action-RF-ontvangers
 *  status1 -> RF-ontvanger 1
 *  status2 -> RF-ontvanger 2
 *  status3 -> RF-ontvanger 3
 */
bool status1 = false;
bool status2 = false;
bool status3 = false;

bool cMode = false;

bool pinState = false;                     // Variable to store actual pin state
bool rfReceiverStatusForSensorThreshold = false;

byte  lightSensorValue = 0;                // Variable to store actual lightsensor value
byte  temperatureSensorValue = 0;          // Variable to store actual temperaturesensor value

byte  lightSensorMinTresholdValue = 12;    // Value of the lightsensor at which we decide to turn a switch off in some later situations
byte  lightSensorMaxTresholdValue = 16;    // Value of the lightsensor at which we decide to turn a switch on in some later situations

void setup()
{
   Serial.begin(9600);
   Serial.println("Start setup()");
   
   pinMode(RFPin, OUTPUT);
   pinMode(ledPin, OUTPUT);
   //Default states
   digitalWrite(RFPin, LOW);
   digitalWrite(ledPin, LOW);

   Serial.println("Obtain IP from DHCP server");
	 //Try to get an IP address from the DHCP server.
	 if (Ethernet.begin(mac) == 0)
	 {
	    Serial.println("Could not obtain IP-address from DHCP -> do nothing");
      Serial.println("Fix network and reboot arduino");
		  while (true){     // no point in carrying on, so do nothing forevermore; check your router
		    delay(1000);    // prevent the microprocessor from being caught on fire!
		  }
	 }

   Serial.println("Domotica project, Arduino server");
   Serial.print("RF-transmitter (click-on click-off Device) on pin ");
   Serial.println(RFPin);
   Serial.print("LED (for connect-state and pin-state) on pin ");
   Serial.println(ledPin);
   Serial.println("Ethernetboard connected (pins 10, 11, 12, 13 and SPI)");
   Serial.println("Connect to DHCP source in local network (blinking led -> waiting for connection)");
   
	 //Start the ethernet server.
   Serial.print("Listening address: ");
   Serial.println(Ethernet.localIP());
   Serial.print("Binding to port: ");
   Serial.println(ethPort);
	 server.begin();

   // for hardware debug: LED indication of server state: blinking = waiting for connection
   int offset = 0; 
   if (getIPClassB(Ethernet.localIP()) == 1) offset = 100;             // router S. Oosterhaven
   int IPnr = getIPComputerNumberOffset(Ethernet.localIP(), offset);   // Get computernumber in local network 192.168.1.105 -> 5)

   // Initialise RF-transmiter
   mySwitch.enableTransmit(RFPin);
   mySwitch.setRepeatTransmit(2);
   sendRF(2379297); // Default state -> false -> off. Send signal to turn off all RF-recievers.
   
   Serial.println("Done with setup(), going to loop()");
   Serial.println("Waiting for client to connect...");
}

 
void sendRF(long code) //stuurt een betrouwbaarder rf signaal (verbetert resultaat)
{
  Serial.print("Sending code on 433.9MhZ band [");
  Serial.print(code);
  Serial.println("]");

  /*
   * Send code multiple times with delays inbetween
   * in order to have a greater possibility for the
   * recievers to recieve the signal.
   */
  mySwitch.send(code, 24);
  delay(175);
  mySwitch.send(code, 24);
  delay(175);
  mySwitch.send(code, 24);
}


void loop()
{
   // Listen for incomming connection (app)
	 EthernetClient ethernetClient = server.available();
	 if (!ethernetClient) {
      blink(ledPin);
	    return; // wait for connection and blink LED
	 }

   Serial.println("Application connected");
   digitalWrite(ledPin, LOW); 


   // RESET states
   sendRF(2379297); // Send ALL OFF code to RF-recievers 
   status1 = false;
   status2 = false;
   status3 = false;

      
   // Do what needs to be done while the socket is connected. (ominous comment is ominous)
	 while (ethernetClient.connected()) 
	 {
      /*
       * This loop executes in a loop as long as
       * an app is connected with the arduino.
       */
      lightSensorValue = readSensor(analogPinLight, 100); // lightSensorValue =>  [0 -> 99]
      temperatureSensorValue = readSensor(analogPinTemperature, 100); // temperatureSensorValue => [0 -> 99]
      
    
      /*
       * Execute command(s) from app
       */
	    while (ethernetClient.available())
	  	{
	  	   char inByte = ethernetClient.read(); // Get byte from the client.
         Serial.print("Recieved command: ");
         Serial.println(inByte);
	  	   executeCommand(inByte);
	    }
     
   }
	 Serial.println("Application disconnected");
   Serial.println("Waiting for client to connect...");
}



// Implementation of (simple) protocol between app and Arduino
// Request (from app) is single char ('a', 's', etc.)
// Response (to app) is 4 chars
void executeCommand(char cmd)
{     
         char response[4] = {'\0', '\0', '\0', '\0'};

         // Command protocol
         Serial.print("[");
         Serial.print(cmd);
         Serial.print("] -> ");
         
         switch (cmd) {
         case 'a': // Report lightsensor value to the app  
            intToCharBuf(lightSensorValue, response, 4);                // convert to charbuffer
            server.write(response, 4);                             // response is always 4 chars (\n included)
            Serial.print("LightSensor: ");
            Serial.println(response);
            break;
         case 'b': // Report temperaturesensor value to the app  
            intToCharBuf(temperatureSensorValue, response, 4);                // convert to charbuffer
            server.write(response, 4);                              // response is always 4 chars (\n included)
            Serial.print("TemperatureSensor: ");
            Serial.println(response);
            break;
                
         case '1': //Turns first RF-Switch on or off, also updates the Status (on od off) after a succesful send.
            status1 = !status1;
            if(status1)
            {
              sendRF(2379311);
              server.write("AAN\n"); 
            }else
            {
              sendRF(2379310);
              server.write("UIT\n");
            }
            break; 
         case '2': //Turns second RF-Switch on or off, also updates the Status (on od off) after a succesful send.
            status2 = !status2;
            if(status2)
            {
              sendRF(2379309);
              server.write("AAN\n"); 
            }else
            {
              sendRF(2379308);
              server.write("UIT\n");
            }
            break;
         
         case '3': //Turns third RF-Switch on or off, also updates the Status (on od off) after a succesful send.
            status3 = !status3;
            if(status3)
            {
              sendRF(2379307);
              server.write("AAN\n"); 
            }else
            {
              sendRF(2379306);
              server.write("UIT\n");
            }
            break;
         case 's': // Report switch state to the app
            if (pinState)
            {
              Serial.println("Pin state is ON");
              server.write("AAN\n");
            }
            else
            {
              Serial.println("Pin state is OFF");
              server.write("UIT\n");
            }
            break;
         case 't': // Toggle state; If state is already ON then turn it OFF
            pinState = !pinState; // true -> false, false -> true
            digitalWrite(ledPin, pinState); // true -> ON, false -> OFF
            Serial.print("Set pin state to ");
            Serial.println(pinState);
            if(pinState)
            {
              server.write("AAN\n");
            }
            else
            {
              server.write("UIT\n");
            }
            break;
         case 'c':
            // Normale C-modes
            rfReceiverStatusForSensorThreshold = false;
            sendRF(2379310);
            status1 = false;
            while(true)
            {
              lightSensorValue = readSensor(analogPinLight, 100); // lightSensorValue =>  [0 -> 99]

              if(!rfReceiverStatusForSensorThreshold && lightSensorValue >= lightSensorMaxTresholdValue)
              {
                rfReceiverStatusForSensorThreshold = true;
                sendRF(2379311);
                status1 = true;
              }
              
              if(rfReceiverStatusForSensorThreshold && lightSensorValue <= lightSensorMinTresholdValue)
              {
                sendRF(2379310); // Turn OFF rfreciever 1.
                rfReceiverStatusForSensorThreshold = false;
                status1 = false;
                break;
              } 
            }
            server.write("NOP\n");
            break;
         case 'C':
            // Wij-beginnen-C-modes
            sendRF(2379311);
            status1 = true;
            
            while(true)
            {
              lightSensorValue = readSensor(analogPinLight, 100); // lightSensorValue =>  [0 -> 99]

              if(lightSensorValue >= lightSensorMaxTresholdValue)
              {
                sendRF(2379310);
                status1 = false;
                break;
              }
            }
            server.write("NOP\n");
            break;
         default:
            server.write("NOP\n"); // No-operation.
         }
         
}

// read value from pin pn, return value is mapped between 0 and mx-1
int readSensor(int pn, int mx)
{
  return map(analogRead(pn), 0, 1023, 0, mx-1);    
}

// Convert int <val> char buffer with length <len>
void intToCharBuf(int val, char buf[], int len)
{
   String s;
   s = String(val);                        // convert tot string
   if (s.length() == 1) s = "0" + s;       // prefix redundant "0" 
   if (s.length() == 2) s = "0" + s;  
   s = s + "\n";                           // add newline
   s.toCharArray(buf, len);                // convert string to char-buffer
}


// blink led on pin <pn>
void blink(int pn)
{
  digitalWrite(pn, HIGH);
  delay(100);
  digitalWrite(pn, LOW);
  delay(100);
}

// Visual feedback on pin, based on IP number
// Blink ledpin for a short burst, then blink N times, where N is (related to) IP-number
void signalNumber(int pin, int n)
{
   int i;
   for (i = 0; i < 30; i++)
   {
      digitalWrite(pin, HIGH);
      delay(20);
      digitalWrite(pin, LOW);
      delay(20);
   }
   delay(1000);
   for (i = 0; i < n; i++)
   {
      digitalWrite(pin, HIGH);
      delay(300);
      digitalWrite(pin, LOW);
      delay(300);
   }
   delay(1000);
}

// Convert IPAddress tot String (e.g. "192.168.1.105")
String IPAddressToString(IPAddress address)
{
    return String(address[0]) + "." + 
           String(address[1]) + "." + 
           String(address[2]) + "." + 
           String(address[3]);
}

// Returns B-class network-id: 192.168.1.105 -> 1)
int getIPClassB(IPAddress address)
{
    return address[2];
}

// Returns computernumber in local network: 192.168.1.105 -> 105)
int getIPComputerNumber(IPAddress address)
{
    return address[3];
}

// Returns computernumber in local network: 192.168.1.105 -> 5)
int getIPComputerNumberOffset(IPAddress address, int offset)
{
    return getIPComputerNumber(address) - offset;
}
