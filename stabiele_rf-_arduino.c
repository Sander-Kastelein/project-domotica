#include <RCSwitch.h>

RCSwitch mySwitch = RCSwitch();
char buttonPin = 2;
bool buttonState = false;

void setup() {
 Serial.begin(9600);
 mySwitch.enableTransmit(10);
 mySwitch.setRepeatTransmit(2);
 pinMode(buttonPin, INPUT);  
 pinMode(13, OUTPUT);
}

void sendRF(long code)
{
  mySwitch.send(code, 24);
  delay(175);
  mySwitch.send(code, 24);
  delay(175);
  mySwitch.send(code, 24);
  delay(175);
  mySwitch.send(code, 24);
}

void allOn()
{
  sendRF(2379298);
}

void allOff()
{
  sendRF(2379297);
}


void buttonClick()
{
  if(buttonState) return allOff();
  allOn();
}

void loop()
{
  if(digitalRead(2))
  {
    buttonState = !buttonState;
    buttonClick();
  }
}
