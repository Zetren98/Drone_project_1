#include <ESP8266WiFi.h>
extern "C" {
  #include <espnow.h>
  #include <user_interface.h>
}
#include <string.h>

#define WIFI_CHANNEL 1
#define UART_BAUD    115200
uint8_t receiverMac[6] = {0x00, 0x00, 0x00, 0x00, 0x00, 0x00}; //Enter the receiver's MAC address
typedef struct __attribute__((packed)) {
  uint8_t  magic;
  uint16_t ch[6];
} espnow_packet_t;

enum { WAIT_SYNC1, WAIT_SYNC2, READ_PAYLOAD };
uint8_t parseState = WAIT_SYNC1;
uint8_t rxBuf[13];
uint8_t rxIdx = 0;

void sendToReceiver(uint16_t *ch) {
  espnow_packet_t pkt;
  pkt.magic = 0xC5;
  memcpy(pkt.ch, ch, sizeof(pkt.ch));
  esp_now_send(receiverMac, (uint8_t*)&pkt, sizeof(pkt));
}

void handleByte(uint8_t b) {
  switch (parseState) {
    case WAIT_SYNC1:
      if (b == 0xAA) parseState = WAIT_SYNC2;
      break;

    case WAIT_SYNC2:
      if (b == 0x55) { parseState = READ_PAYLOAD; rxIdx = 0; }
      else parseState = WAIT_SYNC1;
      break;

    case READ_PAYLOAD:
      rxBuf[rxIdx++] = b;
      if (rxIdx >= 13) {
        parseState = WAIT_SYNC1;

        uint8_t chk = 0;
        for (int i = 0; i < 12; i++) chk ^= rxBuf[i];

        if (chk == rxBuf[12]) {
          uint16_t ch[6];
          for (int i = 0; i < 6; i++) {
            ch[i] = rxBuf[i * 2] | (rxBuf[i * 2 + 1] << 8);
          }
          sendToReceiver(ch);
        }
      }
      break;
  }
}

void setup() {
  Serial.begin(UART_BAUD);

  WiFi.mode(WIFI_STA);
  WiFi.disconnect();
  wifi_set_channel(WIFI_CHANNEL);

  esp_now_init();
  esp_now_set_self_role(ESP_NOW_ROLE_CONTROLLER);
  esp_now_add_peer(receiverMac, ESP_NOW_ROLE_SLAVE, WIFI_CHANNEL, NULL, 0);
}

void loop() {
  while (Serial.available()) {
    handleByte((uint8_t)Serial.read());
  }
}