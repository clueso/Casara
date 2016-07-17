// Designations for the GPS shield
#include <Adafruit_GPS.h>             // Load libraries for GPS
#include <SoftwareSerial.h>

#define GPSECHO  false                // Set GPSECHO to 'false' to turn off echoing the GPS data to the Serial console

void useInterrupt(boolean);           // Func prototype keeps Arduino 0023 happy

boolean usingInterrupt = false;       // This keeps track of whether we're using the interrupt
SoftwareSerial mySerial(8, 7);        // Set pins 7 (RX) and 8 (TX)
Adafruit_GPS GPS(&mySerial);

//Designations for the LED audio strength meter
const int ledCount = 8;               // The number of LEDs in the bar
int ledPins[] = {
  2, 3, 4, 5, 6, 9, 10, 11};          // An array of pin numbers to which LEDs are attached

//Designations for ADC
volatile int adc_pin;
volatile int readFlag;                // High when a value is ready to be read
const int ADC_CHANNELS = 4;           // Set how many analogue inputs to read, starting from A0
int maximum;                          // Audio strength value after ADC

// Create anarray to store the results of the ADC conversions
// 0 - battery, 1 - audio, 2 - strength, 3 - direction
volatile unsigned int myvar[ADC_CHANNELS];
int unsigned Sums[ADC_CHANNELS] = {0};
int unsigned Counts[ADC_CHANNELS] = {0};

void ZeroSumCount()
{
  int i;
  for (i = 0; i < ADC_CHANNELS; i++) {
    Sums[i] = 0;
    Counts[i] = 0;
  }
}

// Initialization
void setup(){
  Serial.begin(115200);
  GPS.begin(9600);
    
  DIDR0 |= _BV(ADC5D);
  DIDR0 |= _BV(ADC4D);
  DIDR0 |= _BV(ADC3D);
  DIDR0 |= _BV(ADC2D);
  DIDR0 |= _BV(ADC1D);
  DIDR0 |= _BV(ADC0D);
  
  // Set up ADC
  ADMUX &= B11011111;                  // Clear ADLAR in ADMUX (0x7C) to right-adjust the result
  ADMUX |= B01000000;                  // Set REFS1..0 in ADMUX (0x7C) to change reference voltage
  ADMUX &= B11110000;                  // Clear MUX3..0 in ADMUX (0x7C) in preparation for setting the analog input
  ADCSRA |= B10000000;                 // Set ADEN in ADCSRA (0x7A) to enable the ADC (12 ADC clocks to execute)
//  ADCSRA |= B00100000;                 // Set ADATE in ADCSRA (0x7A) to enable auto-triggering
//  ADCSRB &= B11111000;                 // Clear ADTS2..0 in ADCSRB (0x7B) to set trigger mode to free running
  ADCSRA |= B00000111;                 // Set the Prescaler to 128 (16000KHz/128 = 125KHz)
  ADCSRA |= B00001000;                 // Set ADIE in ADCSRA (0x7A) to enable the ADC interrupt
  readFlag = 0;                        // Set flag for the first ADC
  
  // Set up Timer1 interrupt
  TCCR1A = 0;                          // Set entire TCCR1A register to 0
  TCCR1B = 0;                          // Set entire TCCR1B register to 0
  TCNT1  = 0;                          // Initialize counter value to 0
  OCR1A = 7812;                        // Set 2Hz interrupt: 7811.5 = (16*10^6) / (2*1024) - 1 (must be <65536)
  TCCR1B |= (1 << WGM12);              // Turn on CTC mode
  TCCR1B |= (1 << CS12) | (1 << CS10); // Set CS12 and CS10 bits for 1024 prescaler
  TIMSK1 |= (1 << OCIE1A);             // Enable timer compare interrupt

  // Set up LEDs
  for (int thisLed = 0; thisLed < ledCount; thisLed++)
    pinMode(ledPins[thisLed], OUTPUT);

  // Set up GSPS
  GPS.sendCommand(PMTK_SET_NMEA_OUTPUT_RMCGGA); // RMC (recommended minimum) and GGA (fix data) including altitude
  GPS.sendCommand(PMTK_SET_NMEA_UPDATE_1HZ);    // Set the update rate at 1Hz
  GPS.sendCommand(PGCMD_ANTENNA);               // Request updates on antenna status
  useInterrupt(true);
  sei();                               // Enable global interrupts  
  ADCSRA |=B01000000;                  // Set ADSC in ADCSRA (0x7A) to start the ADC conversion
  adc_pin = 0;
}

// Processor loop
void loop(){
  int SampledLine;
  // If the ADC has read the audio signal strength
  // update the LEDs.
  if (readFlag == 1) {
    if (adc_pin == 0)
      SampledLine = 3;
    else 
      SampledLine = adc_pin-1;
      
    Sums[SampledLine] += myvar[SampledLine];

    if (SampledLine == 1) {
      if (myvar[1] != 0)
        Counts[1] += 1;
      if (myvar[1] > maximum)
        maximum = myvar[1];                // Set maximum value from pin A1
      clipping();                        // Measure maximum audio signal and display on LEDs
    }
    else
      Counts[SampledLine] += 1;
      
    readFlag = 0;                      // Reset flag
  }

  if (GPS.newNMEAreceived()) {         // If a sentence is received, we can check the checksum, parse it...
    if (!GPS.parse(GPS.lastNMEA()))    // This also sets the newNMEAreceived() flag to false
      return;                          // We can fail to parse a sentence in which case we should just wait for another
  }
}

// Find maximum audio signal and display on LEDs
void clipping () {
  int ledLevel = map(maximum, 0, 1000, 0, ledCount);
  for (int thisLed = 0; thisLed < ledCount; thisLed++) {
    if (thisLed < ledLevel)
      digitalWrite(ledPins[thisLed], HIGH);
    else
      digitalWrite(ledPins[thisLed], LOW);
  }
}

// Interrupt Service Routine for the ADC completion
ISR(ADC_vect){
  (myvar[adc_pin]) = ADCL | (ADCH << 8);         // Must read low first
  if (++adc_pin >= ADC_CHANNELS)
    adc_pin=0; 
  readFlag = 1;                            // Done reading
  ADMUX = (0 << ADLAR) | (1 << REFS0) | adc_pin; // Select ADC Channel
  ADCSRA |=B01000000;                      // Set ADSC in ADCSRA (0x7A) to start the ADC conversion
}

// Interrupt Service Routine for Timer1
ISR(TIMER1_COMPA_vect){  
  Serial.print(myvar[0]);
  Serial.print(",");
  Serial.print(Sums[1]/Counts[1]);
  Serial.print(",");
  Serial.print(maximum);
  Serial.print(",");
  Serial.print(Sums[2]/Counts[2]);
  Serial.print(",");
  Serial.print(Sums[3]/Counts[3]);
  
  if (GPS.fix) {
    Serial.print(","); 
    Serial.print(GPS.latitudeDegrees, 4);
    Serial.print(","); 
    Serial.println(GPS.longitudeDegrees, 4);
  }
  else
    Serial.println();
  maximum = 0;
  ZeroSumCount();
}

// Interrupt is called once a millisecond, looks for any new GPS data, and stores it
SIGNAL(TIMER0_COMPA_vect) {
  char c = GPS.read();
}

void useInterrupt(boolean v) {
  if (v) {
    // Timer0 is already used for millis() - we'll just interrupt somewhere in the middle and call the "Compare A" function above
    OCR0A = 0xAF;
    TIMSK0 |= _BV(OCIE0A);
    usingInterrupt = true;
  } 
  else {
    // Do not call the interrupt function COMPA anymore
    TIMSK0 &= ~_BV(OCIE0A);
    usingInterrupt = false;
  }
}

// 1. Not sure why myvar[0] = A3, myvar[1] = A0, myvar[2] = A1, myvar[3] = A2
// 2. Timer1 set for 2Hz, but not observing that frequency in serial data output
// 3. Should change maximum to average and smooth out signal
