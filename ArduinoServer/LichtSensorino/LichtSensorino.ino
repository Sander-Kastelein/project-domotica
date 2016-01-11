#include <RemoteTransmitter.h> //include de librairy die bij jouw schakelaar hoort.
 
#define sensorPin 0
#define RFPin 3
 
int sensorReading = 0;
int thresholdValue = 155; //verander naar afstelling van je lichtsensor.
bool isSwitchOn = false;
ElroTransmitter elroTransmitter(RFPin); //vervang dit met de transmitter die bij jouw schakelaar hoort.
 
void setup() {
  Serial.begin(9600);
  pinMode(3, OUTPUT);
 
}
 
//deze code gaat er vanuit dat je schakelaar aan het begin UIT staat.
void loop() {
  sensorReading = analogRead(sensorPin);
  Serial.print("Sensor reading: "); Serial.println(sensorReading);
  if(sensorReading >= thresholdValue) //ik ga hiervan uit van een ldr, als ik licht op de sensor schijnt wordt de waarde lager; is dat bij jullie niet het geval verander "<=" naar ">="
  {
    digitalWrite(3, HIGH);   // turn the LED on (HIGH is the voltage level) 
  }else{
     digitalWrite(3, LOW); 
  }

}
