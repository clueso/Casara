// Designations for the GPS shield
#include <Adafruit_GPS.h>             // Load libraries for GPS
#include <SoftwareSerial.h>

#define GPSECHO  false                // Set GPSECHO to 'false' to turn off echoing the GPS data to the Serial console
#define LED_COUNT  8                  // The number of LEDs in the bar
void useInterrupt(boolean);           // Func prototype keeps Arduino 0023 happy

boolean usingInterrupt = false;       // This keeps track of whether we're using the interrupt
SoftwareSerial mySerial(8, 7);        // Set pins 7 (RX) and 8 (TX)
Adafruit_GPS GPS(&mySerial);

//Designations for the LED audio strength meter
//const int ledCount = 8;               // The number of LEDs in the bar
int ledPins[] = {
  2, 3, 4, 5, 6, 9, 10, 11};          // An array of pin numbers to which LEDs are attached

//Designations for ADC
volatile int i;
volatile int readFlag;                // High when a value is ready to be read
const int ADC_CHANNELS = 4;           // Set how many analogue inputs to read, starting from A0
int maximum;                          // Audio strength value after ADC

// Create an array to store the results of the ADC conversions
// 0 - battery, 1 - audio, 2 - strength, 3 - direction
volatile unsigned int myvar[ADC_CHANNELS];
int unsigned Sums[ADC_CHANNELS] = {0};
int unsigned Counts[ADC_CHANNELS] = {0};
bool gps_sentence_received;
bool transmit_ready;

void ZeroSumCount()
{
  int i;
  for (i = 0; i < ADC_CHANNELS; i++) {
    Sums[i] = 0;
    Counts[i] = 0;
  }
}

unsigned int set_timer_value(int Hz)
{
  long divisor = Hz * 1024L;
  return (unsigned int)((16L*pow(10,6))/divisor);
}

int str_to_int(char *str)
{
  return (str[0] - '0') * 10 + (str[1] - '0');
}

void handle_serial_read()
{
  char buf[10];

  if (!Serial.available())
    return;

  Serial.readBytesUntil('*', buf, 20);
  if (buf[0] == 'f')
    OCR1A = set_timer_value(str_to_int(buf+1));
}

// 0 - battery, 1 - audio, 2 - strength, 3 - direction
void transmit_data()
{
  //Instantaneous value of Battery
  Serial.print(myvar[0]);
  Serial.print(",");
 
 //Average value of Audio
  Serial.print(Sums[1]/Counts[1]);
  Serial.print(",");
  //Maximum Audio value
  //Serial.print(maximum);
  //Serial.print(",");
  
  //Average value strength
  //Serial.print(Sums[2]/Counts[2]);
  //Serial.print(",");
  
  //Instantaneous value of strength reading
  Serial.print(myvar[2]);
  Serial.print(",");
  //Average value of direction
  //Serial.print(Sums[3]/Counts[3]);
  //Serial.print(",");
  
  //Instantaneous value of direction
  Serial.print(myvar[3]);
  
  if (GPS.fix && gps_sentence_received) {
    Serial.print(","); 
    Serial.print(GPS.latitudeDegrees, 4);
    Serial.print(",");
    Serial.print(GPS.longitudeDegrees, 4);
    Serial.print(",");
    Serial.println(GPS.altitude);
    gps_sentence_received = false;
  }
  else {
    Serial.println(",,,");
  }
  maximum = 0;
  ZeroSumCount();
  transmit_ready = false;
}

// Initialization
void setup(){
  Serial.begin(115200);
  GPS.begin(9600);
    
  readFlag = 0;                        // Set flag for the first ADC
  maximum = 0;
  
  // Set up Timer1 interrupt
  TCCR1A = 0;                          // Set entire TCCR1A register to 0
  TCCR1B = 0;                          // Set entire TCCR1B register to 0
  TCNT1  = 0;                          // Initialize counter value to 0
  OCR1A = set_timer_value(20);
  TCCR1B |= (1 << WGM12);              // Turn on CTC mode
  TCCR1B |= (1 << CS12) | (1 << CS10); // Set CS12 and CS10 bits for 1024 prescaler
  TIMSK1 |= (1 << OCIE1A);             // Enable timer compare interrupt

  // Set up LEDs
  for (int thisLed = 0; thisLed < LED_COUNT; thisLed++)
    pinMode(ledPins[thisLed], OUTPUT);

  // Set up GSPS
  GPS.sendCommand(PMTK_SET_NMEA_OUTPUT_RMCGGA); // RMC (recommended minimum) and GGA (fix data) including altitude
  GPS.sendCommand(PMTK_SET_NMEA_UPDATE_1HZ);    // Set the update rate at 1Hz
  GPS.sendCommand(PGCMD_ANTENNA);               // Request updates on antenna status
  useInterrupt(true);
  sei();                               // Enable global interrupts  
  ADCSRA |=B01000000;                  // Set ADSC in ADCSRA (0x7A) to start the ADC conversion
  gps_sentence_received = false;
  transmit_ready = false;
}

// Processor loop
void loop(){
  int i;
  // If the ADC has read the audio signal strength
  // update the LEDs.
  
  for (i = 0; i < ADC_CHANNELS; i++) {
    analogRead(i);
    myvar[i] = analogRead(i);
    switch(i) {
      case 0:
        Sums[i] += myvar[i];
        Counts[i] += 1;
        break;
      case 1:
        if (myvar[i] != 0) {
          Sums[i] += myvar[i];
          Counts[i] += 1;
        }
        if (myvar[1] > maximum)
          maximum = myvar[1];                // Set maximum value from pin A1
        break;
      case 2:
        Sums[i] += myvar[i];
        Counts[i] += 1;
        break;
      case 3:
        break;
      default:
        break;
    }
  }  

  clipping();                        // Measure maximum audio signal and display on LEDs
        
  if (GPS.newNMEAreceived()) {         // If a sentence is received, we can check the checksum, parse it...
    if (!GPS.parse(GPS.lastNMEA()))    // This also sets the newNMEAreceived() flag to false
      return;      // We can fail to parse a sentence in which case we should just wait for another
    gps_sentence_received = true;
  }
  
  if (transmit_ready)
    transmit_data();
}

// Find maximum audio signal and display on LEDs
void clipping () {
  int ledLevel = map(maximum, 0, 1023, 0, LED_COUNT);
  
  for (int thisLed = 0; thisLed < ledLevel; thisLed++)
    if (thisLed < ledLevel)
      digitalWrite(ledPins[thisLed], HIGH);
    else
      digitalWrite(ledPins[thisLed], LOW);
}

// Interrupt Service Routine for Timer1
ISR(TIMER1_COMPA_vect){
  transmit_ready = true;
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
