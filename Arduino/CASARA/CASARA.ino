#include <Adafruit_GPS.h>
#include <SoftwareSerial.h>

// If you're using the Adafruit GPS shield, change 
// SoftwareSerial mySerial(3, 2); -> SoftwareSerial mySerial(8, 7);
// and make sure the switch is set to SoftSerial

// If using software serial, keep this line enabled
// (you can change the pin numbers to match your wiring):
SoftwareSerial mySerial(13, 12);

// If using hardware serial (e.g. Arduino Mega), comment out the
// above SoftwareSerial line, and enable this line instead
// (you can change the Serial number to match your wiring):

//HardwareSerial mySerial = Serial1;


Adafruit_GPS GPS(&mySerial);


// Set GPSECHO to 'false' to turn off echoing the GPS data to the Serial console
// Set to 'true' if you want to debug and listen to the raw GPS sentences. 
#define GPSECHO  false

// this keeps track of whether we're using the interrupt
// off by default!
boolean usingInterrupt = false;
void useInterrupt(boolean); // Func prototype keeps Arduino 0023 happy

const int ledCount = 8;    // the number of LEDs in the bar graph
int ledPins[] = {
  2, 3, 4, 5, 6, 9, 10, 11}; // an array of pin numbers to which LEDs are attached
int maximum;
// High when a value is ready to be read
volatile int readFlag;
// Value to store analog result
volatile int analogVal;
//timer1 will interrupt at 1Hz
boolean toggle1 = 0;

void setup(){
  // clear ADLAR in ADMUX (0x7C) to right-adjust the result
  // ADCL will contain lower 8 bits, ADCH upper 2 (in last two bits)
  ADMUX &= B11011111;

  // Set REFS1..0 in ADMUX (0x7C) to change reference voltage to the
  // proper source (01)
  ADMUX |= B01000000;

  // Clear MUX3..0 in ADMUX (0x7C) in preparation for setting the analog
  // input
  ADMUX &= B11110000;

  // Set MUX3..0 in ADMUX (0x7C) to read from AD8 (Internal temp)
  // Do not set above 15! You will overrun other parts of ADMUX. A full
  // list of possible inputs is available in Table 24-4 of the ATMega328
  // datasheet
  ADMUX |= 1;

  // ADMUX |= B00001000; // Binary equivalent
  // Set ADEN in ADCSRA (0x7A) to enable the ADC.
  // Note, this instruction takes 12 ADC clocks to execute
  ADCSRA |= B10000000;

  // Set ADATE in ADCSRA (0x7A) to enable auto-triggering.
  ADCSRA |= B00100000;

  // Clear ADTS2..0 in ADCSRB (0x7B) to set trigger mode to free running.
  // This means that as soon as an ADC has finished, the next will be
  // immediately started.
  ADCSRB &= B11111000;

  // Set the Prescaler to 128 (16000KHz/128 = 125KHz)
  // Above 200KHz 10-bit results are not reliable.
  ADCSRA |= B00000111;

  // Set ADIE in ADCSRA (0x7A) to enable the ADC interrupt.
  // Without this, the internal interrupt will not trigger.
  ADCSRA |= B00001000;

  // Enable global interrupts
  // AVR macro included in <avr/interrupts.h>, which the Arduino IDE
  // supplies by default.
  sei();

  // Kick off the first ADC
  readFlag = 0;

  // Set ADSC in ADCSRA (0x7A) to start the ADC conversion
  ADCSRA |=B01000000;

  //set timer1 interrupt at 1Hz
  //pinMode(13, OUTPUT);
  cli();//stop interrupts
  TCCR1A = 0;// set entire TCCR1A register to 0
  TCCR1B = 0;// same for TCCR1B
  TCNT1  = 0;//initialize counter value to 0
  // set compare match register for 1hz increments
  OCR1A = 15624;// = (16*10^6) / (1*1024) - 1 (must be <65536)
  // turn on CTC mode
  TCCR1B |= (1 << WGM12);
  // Set CS12 and CS10 bits for 1024 prescaler
  TCCR1B |= (1 << CS12) | (1 << CS10);  
  // enable timer compare interrupt
  TIMSK1 |= (1 << OCIE1A);
  sei();//allow interrupts

  for (int thisLed = 0; thisLed < ledCount; thisLed++) {
    pinMode(ledPins[thisLed], OUTPUT);
  }

  Serial.begin(115200);
  // 9600 NMEA is the default baud rate for Adafruit MTK GPS's- some use 4800
  GPS.begin(9600);

  // uncomment this line to turn on RMC (recommended minimum) and GGA (fix data) including altitude
  GPS.sendCommand(PMTK_SET_NMEA_OUTPUT_RMCGGA);
  // uncomment this line to turn on only the "minimum recommended" data
  //GPS.sendCommand(PMTK_SET_NMEA_OUTPUT_RMCONLY);
  // For parsing data, we don't suggest using anything but either RMC only or RMC+GGA since
  // the parser doesn't care about other sentences at this time

  // Set the update rate
  GPS.sendCommand(PMTK_SET_NMEA_UPDATE_1HZ);   // 1 Hz update rate
  // For the parsing code to work nicely and have time to sort thru the data, and
  // print it out we don't suggest using anything higher than 1 Hz

  // Request updates on antenna status, comment out to keep quiet
  GPS.sendCommand(PGCMD_ANTENNA);

  // the nice thing about this code is you can have a timer0 interrupt go off
  // every 1 millisecond, and read data from the GPS for you. that makes the
  // loop code a heck of a lot easier!
  useInterrupt(true);

//  delay(1000);
  // Ask for firmware version
//  mySerial.println(PMTK_Q_RELEASE);
}

// Interrupt service routine for the ADC completion
ISR(ADC_vect){
  // Done reading
  readFlag = 1;

  // Must read low first
  analogVal = ADCL | (ADCH << 8);

  // Not needed because free-running mode is enabled.
  // Set ADSC in ADCSRA (0x7A) to start another ADC conversion
  // ADCSRA |= B01000000;
}

ISR(TIMER1_COMPA_vect){//timer1 interrupt 1Hz toggles pin 13 (LED)
  //generates pulse wave of frequency 1Hz/2 = 0.5Hz (takes two cycles for full wave- toggle high then toggle low)
  if (toggle1){
    //digitalWrite(13,HIGH);
    Serial.print(maximum);
    if (GPS.fix) {
      Serial.print(", "); 
      Serial.print(GPS.latitudeDegrees, 4);
      Serial.print(", "); 
      Serial.println(GPS.longitudeDegrees, 4);
    }
    maximum = 0;
    toggle1 = 0;
  }
  else{
    //digitalWrite(13,LOW);
    toggle1 = 1;
  }
}

// Interrupt is called once a millisecond, looks for any new GPS data, and stores it
SIGNAL(TIMER0_COMPA_vect) {
  char c = GPS.read();
  // if you want to debug, this is a good time to do it!
#ifdef UDR0
  if (GPSECHO)
    if (c) UDR0 = c;  
  // writing direct to UDR0 is much much faster than Serial.print 
  // but only one character can be written at a time. 
#endif
}

void useInterrupt(boolean v) {
  if (v) {
    // Timer0 is already used for millis() - we'll just interrupt somewhere
    // in the middle and call the "Compare A" function above
    OCR0A = 0xAF;
    TIMSK0 |= _BV(OCIE0A);
    usingInterrupt = true;
  } 
  else {
    // do not call the interrupt function COMPA anymore
    TIMSK0 &= ~_BV(OCIE0A);
    usingInterrupt = false;
  }
}

uint32_t timer = millis();

void loop() {
  // Check to see if the value has been updated
  if (readFlag == 1){
    // Perform whatever updating needed
    data();
    clipping();
    readFlag = 0;
  }
  // Whatever else you would normally have running in loop().
  // in case you are not using the interrupt above, you'll
  // need to 'hand query' the GPS, not suggested :(
  if (! usingInterrupt) {
    // read data from the GPS in the 'main loop'
    char c = GPS.read();
    // if you want to debug, this is a good time to do it!
    if (GPSECHO)
      if (c) Serial.print(c);
  }

  // if a sentence is received, we can check the checksum, parse it...
  if (GPS.newNMEAreceived()) {
    // a tricky thing here is if we print the NMEA sentence, or data
    // we end up not listening and catching other sentences! 
    // so be very wary if using OUTPUT_ALLDATA and trytng to print out data
    //Serial.println(GPS.lastNMEA());   // this also sets the newNMEAreceived() flag to false

    if (!GPS.parse(GPS.lastNMEA()))   // this also sets the newNMEAreceived() flag to false
      return;  // we can fail to parse a sentence in which case we should just wait for another
  }

  // if millis() or timer wraps around, we'll just reset it
  if (timer > millis())  timer = millis();

  // approximately every 2 seconds or so, print out the current stats
  if (millis() - timer > 2000) { 
    timer = millis(); // reset the timer

    //    Serial.print("\nTime: ");
    //    Serial.print(GPS.hour, DEC); Serial.print(':');
    //    Serial.print(GPS.minute, DEC); Serial.print(':');
    //    Serial.print(GPS.seconds, DEC); Serial.print('.');
    //    Serial.println(GPS.milliseconds);
    //    Serial.print("Date: ");
    //    Serial.print(GPS.day, DEC); Serial.print('/');
    //    Serial.print(GPS.month, DEC); Serial.print("/20");
    //    Serial.println(GPS.year, DEC);
    //    Serial.print("Fix: "); Serial.print((int)GPS.fix);
    //    Serial.print(" quality: "); Serial.println((int)GPS.fixquality); 
    if (GPS.fix) {
      //      Serial.print("Location: ");
      //      Serial.print(GPS.latitude, 4); Serial.print(GPS.lat);
      //      Serial.print(", "); 
      //      Serial.print(GPS.longitude, 4); Serial.println(GPS.lon);
      //      Serial.print("Location (in degrees, works with Google Maps): ");
      //      Serial.print(GPS.latitudeDegrees, 4);
      //      Serial.print(", "); 
      //      Serial.println(GPS.longitudeDegrees, 4);

      //      Serial.print("Speed (knots): "); Serial.println(GPS.speed);
      //      Serial.print("Angle: "); Serial.println(GPS.angle);
      //      Serial.print("Altitude: "); Serial.println(GPS.altitude);
      //      Serial.print("Satellites: "); Serial.println((int)GPS.satellites);
    }
  }

}

void data() {
  if (analogVal > maximum) {
    maximum = analogVal;
  }
}

void clipping () {
  int ledLevel = map(analogVal, 0, 1000, 0, ledCount);
  for (int thisLed = 0; thisLed < ledCount; thisLed++) {
    if (thisLed < ledLevel) {
      digitalWrite(ledPins[thisLed], HIGH);
    }
    else {
      digitalWrite(ledPins[thisLed], LOW);
    }
  }
}


























