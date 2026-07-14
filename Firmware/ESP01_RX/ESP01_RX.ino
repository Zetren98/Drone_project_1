#include <ESP8266WiFi.h>
extern "C" {
  #include <espnow.h>
  #include <user_interface.h>
}
#include <string.h>
#define WIFI_CHANNEL             1
#define CRSF_BAUD                420000
#define CRSF_UPDATE_INTERVAL_MS  14
#define FAILSAFE_TIMEOUT_MS      500
typedef struct __attribute__((packed)) {
  uint8_t  magic;
  uint16_t ch[6];
} espnow_packet_t;

volatile espnow_packet_t lastPacket;
volatile unsigned long   lastRxMillis = 0;
volatile bool            havePacket = false;

#define CRSF_ADDR_FC           0xC8
#define CRSF_TYPE_RC_CHANNELS  0x16
#define CRSF_RC_FRAME_LEN      24

typedef struct __attribute__((packed)) {
  unsigned ch0:11;  unsigned ch1:11;  unsigned ch2:11;  unsigned ch3:11;
  unsigned ch4:11;  unsigned ch5:11;  unsigned ch6:11;  unsigned ch7:11;
  unsigned ch8:11;  unsigned ch9:11;  unsigned ch10:11; unsigned ch11:11;
  unsigned ch12:11; unsigned ch13:11; unsigned ch14:11; unsigned ch15:11;
} crsfChannels_t;

uint8_t crc8_dvb_s2(const uint8_t *buf, uint8_t len) {
  uint8_t crc = 0;
  for (uint8_t i = 0; i < len; i++) {
    crc ^= buf[i];
    for (uint8_t b = 0; b < 8; b++) {
      crc = (crc & 0x80) ? (uint8_t)((crc << 1) ^ 0xD5) : (uint8_t)(crc << 1);
    }
  }
  return crc;
}

uint16_t usToCrsf(uint16_t us) {
  int32_t v = ((int32_t)us - 1500) * 8 / 5 + 992;
  if (v < 172)  v = 172;
  if (v > 1811) v = 1811;
  return (uint16_t)v;
}

void sendCrsfFrame(uint16_t *crsfVals) {
  uint8_t frame[26];
  frame[0] = CRSF_ADDR_FC;
  frame[1] = CRSF_RC_FRAME_LEN;
  frame[2] = CRSF_TYPE_RC_CHANNELS;

  crsfChannels_t packed;
  packed.ch0  = crsfVals[0];  packed.ch1  = crsfVals[1];  packed.ch2  = crsfVals[2];  packed.ch3  = crsfVals[3];
  packed.ch4  = crsfVals[4];  packed.ch5  = crsfVals[5];  packed.ch6  = crsfVals[6];  packed.ch7  = crsfVals[7];
  packed.ch8  = crsfVals[8];  packed.ch9  = crsfVals[9];  packed.ch10 = crsfVals[10]; packed.ch11 = crsfVals[11];
  packed.ch12 = crsfVals[12]; packed.ch13 = crsfVals[13]; packed.ch14 = crsfVals[14]; packed.ch15 = crsfVals[15];

  memcpy(&frame[3], &packed, 22);
  frame[25] = crc8_dvb_s2(&frame[2], 23);

  Serial.write(frame, 26);
}

void onReceive(uint8_t *mac, uint8_t *data, uint8_t len) {
  if (len == sizeof(espnow_packet_t)) {
    espnow_packet_t *p = (espnow_packet_t *)data;
    if (p->magic == 0xC5) {
      memcpy((void *)&lastPacket, p, sizeof(espnow_packet_t));
      lastRxMillis = millis();
      havePacket = true;
    }
  }
}

unsigned long lastSend = 0;

void setup() {
  Serial.begin(CRSF_BAUD);

  WiFi.mode(WIFI_STA);
  WiFi.disconnect();
  wifi_set_channel(WIFI_CHANNEL);

  esp_now_init();
  esp_now_set_self_role(ESP_NOW_ROLE_SLAVE);
  esp_now_register_recv_cb(onReceive);
}

void loop() {
  unsigned long now = millis();
  if (now - lastSend >= CRSF_UPDATE_INTERVAL_MS) {
    lastSend = now;

    if (havePacket && (now - lastRxMillis) < FAILSAFE_TIMEOUT_MS) {
      uint16_t us[6];
      memcpy(us, (void *)lastPacket.ch, sizeof(us));

      uint16_t crsfVals[16];
      crsfVals[0] = usToCrsf(us[1]);
      crsfVals[1] = usToCrsf(us[2]);
      crsfVals[2] = usToCrsf(us[0]);
      crsfVals[3] = usToCrsf(us[3]);
      crsfVals[4] = usToCrsf(us[4]);
      crsfVals[5] = usToCrsf(us[5]);
      for (int i = 6; i < 16; i++) crsfVals[i] = 992;
      sendCrsfFrame(crsfVals);
    }
  }
}
