/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : Main program body
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2022 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
  
  /**
  
	Sistema de tracking para câmera 4 PRJ
	Elaborado por: Daniel Leite / Fabiano Aguileira
	Software para  captura dos dados da câmera e conversão / envio via FreeD
	Mapa de calibração de distorção de trilhos gravado no progroma. 
	Em caso de mudança fisíca no sistema, será necessário criar um novo mapa.
  
  */

/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include "main.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */
#include <stdio.h>

#include "decoderTrack.h"
#include "w5500_spi.h"
#include "FreeD.h"

#include "config.h"
#include "mapas.h"

/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */

/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */

// Define da porta o intern trace monitor para fazer degug via trace
#define ITM_Port32(n) (*((volatile unsigned long *) (ITM_BASE + 4*n)))

#define UDP_SOCKET1 (uint8_t) 0
#define UDP_SOCKET2 (uint8_t) 1
#define UDP_SOCKET3 (uint8_t) 2
#define UDP_SOCKET4 (uint8_t) 3
#define UDP_SOCKET5 (uint8_t) 4
#define UDP_SOCKET6 (uint8_t) 5
#define UDP_SOCKET7 (uint8_t) 6
#define TCP_SERVER_SOCKET (uint8_t) 7

#define PORTA_RX_TCP_SERVER	1110

#define TIME_OUT_STATUS_SYNC_MS (2*1000/60)
#define TIME_OUT_STATUS_RX_MS 	(2*1000/60)
#define TEMPO_REFRESH_MENU_MS	(1*1000/60)

#define SizeVectorBuffer	3
#define CAM_ID				1
#define FREED_BUFFER_INVALID	0xFFU

#define SEND_IN_INTERRUPCION

//#define DEBUG_ITM
//#define DEFUB_PRINTF
//#define DEBUG_USB
//#define WTD_RESET   // Realiza o reset do wtd dentro da rotina de leitura do serial

//#define CASA

/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/
IWDG_HandleTypeDef hiwdg;

SPI_HandleTypeDef hspi1;

UART_HandleTypeDef huart2;
DMA_HandleTypeDef hdma_usart2_rx;

/* USER CODE BEGIN PV */

configTrack configGlobal;

volatile uint16_t bootPage0Hdr = 0xFFFF;
volatile uint16_t bootPage1Hdr = 0xFFFF;
volatile uint16_t afterEeInitPage0Hdr = 0xFFFF;
volatile uint16_t afterEeInitPage1Hdr = 0xFFFF;
volatile uint16_t bootBtLoadDefault = 0xFFFF;

///************** VARI�?VEIS EEPROM ***************************************/

/* Virtual address defined by the user: 0xFFFF value is prohibited */
uint16_t VirtAddVarTab[NB_OF_VAR];

///**************  BUFFET DE STATUS E SAIDA TCP *********************************/

uint8_t bufferStatusGeral[64];
uint8_t buffetMsgOut[2048];

//************* MAPA DE COMPENSACAO XY  ****************************************/

volatile dadosMapaTrack  mapaST_XY = {   .distComp = 0,
										 .lastPoseVet = 0,
										 .lastPoseVet = -1,
										 .proxPosVet = -1,
										 .vetSize = sizeof(VetMapXY)/(sizeof(float)*3)};

volatile dadosMapaTrack  mapaST_Z = {  	 .distComp = 0,
										 .lastPoseVet = 0,
										 .lastPoseVet = -1,
										 .proxPosVet = -1,
										 .vetSize = sizeof(VetMapZ)/(sizeof(float)*2)};

volatile dadosMapaTrack  mapaST_PAN = {  .distComp = 0,
										 .lastPoseVet = 0,
										 .lastPoseVet = -1,
										 .proxPosVet = -1,
										 .vetSize = sizeof(VetMapaPAN)/(sizeof(float)*2)};

volatile dadosMapaTrack  mapaST_T_ROLL = {	.distComp = 0,
											.lastPoseVet = 0,
											.lastPoseVet = -1,
											.proxPosVet = -1,
											.vetSize = sizeof(VetMapaROLL)/(sizeof(float)*2)};

/////****************************************************************************/

PinCtlW5500 setup_gpio = {	.GPIO_CS = SOFT_CS_ETH_GPIO_Port,
 						 	.PIN_CS = SOFT_CS_ETH_Pin,
 						    .GPIO_RST = SOFT_RST_ETH_GPIO_Port,
 						    .PIN_RST = SOFT_RST_ETH_Pin};

uint8_t ipRemoto1[4], ipRemoto2[4], ipRemoto3[4], ipRemoto4[4], ipRemoto5[4], ipRemoto6[4], ipRemoto7[4];


 uint8_t volatile bufferUdpTX[FREE_D_D1_PACKET_SIZE];

 FreeD_D1_t pacoteFreed = {  .ID = 255,
							 .Pan = 0.0,
							 .Tilt = 0.0,
							 .Roll = 0.0,
							 .X = 0.0,
							 .Y = 0.0,
							 .Z = 0.0,
							 .Zoom = 0,
							 .Focus = 0};

 volatile uint8_t SYNC_ON = 0, idBufferEscrita = 0, RX_STATUS = 0;
 volatile uint32_t dt_sync = 0, dt_rx = 0;
 volatile bufferData bufferRX_SerialCam4[SizeVectorBuffer];
 volatile bufferData bufferRXTemp[SizeVectorBuffer];
 volatile uint8_t cam_id_ct = 0;

 volatile uint32_t contadorFalhaEnvioUDP = 0; // conta quantos envios nao retornaram o tamanho esperado (29)
 volatile uint32_t contadorFalhaEnquadramentoUART = 0; // conta pacotes seriais com tamanho ok mas marcador invalido
 volatile uint32_t contadorSyncTotal = 0;
 volatile uint32_t contadorSyncLost = 0;
 volatile uint32_t ultimoSyncTick = 0;
 volatile uint32_t contadorFreeDTotal = 0;
 volatile uint32_t contadorFreeDOk = 0;
 volatile uint32_t contadorFreeDErro = 0;
 volatile uint32_t contadorFreeDDropped = 0;
 volatile uint32_t ultimoFreeDTick = 0;
 volatile int32_t ultimoFreeDRetorno = 0;
 volatile uint8_t ultimoFreeDSocket = 0;
 volatile uint32_t contadorEthDown = 0;
 volatile uint32_t contadorEthUp = 0;
 volatile uint32_t estadoLinkEthernet = 0;

 /* Instrumentacao observacional do servidor TCP 1110 (socket 7). */
 volatile uint32_t tcpAcceptCount = 0;
 volatile uint32_t tcpRxOkCount = 0;
 volatile uint32_t tcpRxErrCount = 0;
 volatile uint32_t tcpSendCallCount = 0;
 volatile uint32_t tcpSendRetOkCount = 0;
 volatile uint32_t tcpSendRetErrCount = 0;
 volatile uint32_t tcpSendOkCount = 0;
 volatile uint32_t tcpTimeoutCount = 0;
 volatile uint32_t tcpCloseWaitCount = 0;
 volatile uint32_t tcpDisconCount = 0;
 volatile uint32_t tcpReopenCount = 0;
 volatile uint8_t tcpLastSr = SOCK_CLOSED;
 volatile uint8_t tcpLastIr = 0;
 volatile int32_t tcpLastRecv = -1;
 volatile int32_t tcpLastSend = -1;
 volatile uint16_t tcpLastTxFsr = 0;
 volatile uint8_t tcpPreviousSocketState = 0xFFU;
 volatile uint8_t tcpPreviousIr = 0;
 volatile uint8_t tcpSocketEverOpened = 0;
 volatile uint32_t tcpListenCount = 0;
 volatile uint32_t tcpClosedToListenMs = 0;
 volatile uint32_t tcpMaxClosedToListenMs = 0;
 volatile uint32_t tcpClosedObservedTick = 0;
 volatile uint8_t tcpClosedTimingActive = 0;

 volatile int32_t stsSocket0, stsSocket1, stsSocket2, stsSocket3, stsSocket4, stsSocket5, stsSocket6, stsSocket7;

 volatile uint16_t timmerVal;

 volatile uint32_t pmask; // Marcador de interrucoes ativas



 volatile gap_zoom gapLente = { .lastRawValue = 0,
 	 	 	 	 	 	 	 	.backSlashAtual = 0,
 	 	 	 	 	 	 	 	.zoomCorrigido = 0};

 uint32_t dt = 0;

 /**************************************************************************/

 /* Virtual address defined by the user: 0xFFFF value is prohibited */

 /**************************************************************************/

/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
static void MX_GPIO_Init(void);
static void MX_DMA_Init(void);
static void MX_SPI1_Init(void);
static void MX_USART2_UART_Init(void);
static void MX_IWDG_Init(void);
/* USER CODE BEGIN PFP */

void statusBooting(void);

/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */

/* USER CODE END 0 */

/**
  * @brief  The application entry point.
  * @retval int
  */
int main(void)
{
  /* USER CODE BEGIN 1 */

  /* USER CODE END 1 */

  /* MCU Configuration--------------------------------------------------------*/

  /* Reset of all peripherals, Initializes the Flash interface and the Systick. */
  HAL_Init();

  /* USER CODE BEGIN Init */

  /* USER CODE END Init */

  /* Configure the system clock */
  SystemClock_Config();

  /* USER CODE BEGIN SysInit */

  /* USER CODE END SysInit */

  /* Initialize all configured peripherals */
  MX_GPIO_Init();
  MX_DMA_Init();
  MX_SPI1_Init();
  MX_USART2_UART_Init();
  MX_IWDG_Init();
  /* USER CODE BEGIN 2 */

  statusBooting();

  enter_critical();

  configInit(VirtAddVarTab, NB_OF_VAR);				// Inicializa a eeprom para operar com a configuração salva

  loadConfig();										// Carrega a configuração da eeprom na variavel de configuracao global

  exit_critical();

  // Carrega configuracao padrao, caso a chave seja apertada durante a inicializacao
  bootBtLoadDefault = (uint16_t)HAL_GPIO_ReadPin(BT_LOAD_DEFAULT_GPIO_Port, BT_LOAD_DEFAULT_Pin);
  if(bootBtLoadDefault == 0){
	  loadDefault();
  }


  wiz_NetInfo configRede = { .mac = {configGlobal.config.mac1, configGlobal.config.mac2, configGlobal.config.mac3, configGlobal.config.mac4, configGlobal.config.mac5, configGlobal.config.mac6},
								 .ip  = {configGlobal.config.ip1 , configGlobal.config.ip2, configGlobal.config.ip3, configGlobal.config.ip4},
								 .sn =  {configGlobal.config.mask1, configGlobal.config.mask2, configGlobal.config.mask3, configGlobal.config.mask4},
								 .gw =  {configGlobal.config.gw1, configGlobal.config.gw2, configGlobal.config.gw3, configGlobal.config.gw4},
								 .dns = {configGlobal.config.dns1, configGlobal.config.dns2, configGlobal.config.dns3, configGlobal.config.dns4},
								 .dhcp = configGlobal.config.dhcp_static};

  ipRemoto1[0] = configGlobal.config.ip1_rmt1; ipRemoto1[1] =  configGlobal.config.ip2_rmt1; ipRemoto1[2] =  configGlobal.config.ip3_rmt1; ipRemoto1[3] =  configGlobal.config.ip4_rmt1;
  ipRemoto2[0] = configGlobal.config.ip1_rmt2; ipRemoto2[1] =  configGlobal.config.ip2_rmt2; ipRemoto2[2] =  configGlobal.config.ip3_rmt2; ipRemoto2[3] =  configGlobal.config.ip4_rmt2;
  ipRemoto3[0] = configGlobal.config.ip1_rmt3; ipRemoto3[1] =  configGlobal.config.ip2_rmt3; ipRemoto3[2] =  configGlobal.config.ip3_rmt3; ipRemoto3[3] =  configGlobal.config.ip4_rmt3;
  ipRemoto4[0] = configGlobal.config.ip1_rmt4; ipRemoto4[1] =  configGlobal.config.ip2_rmt4; ipRemoto4[2] =  configGlobal.config.ip3_rmt4; ipRemoto4[3] =  configGlobal.config.ip4_rmt4;
  ipRemoto5[0] = configGlobal.config.ip1_rmt5; ipRemoto5[1] =  configGlobal.config.ip2_rmt5; ipRemoto5[2] =  configGlobal.config.ip3_rmt5; ipRemoto5[3] =  configGlobal.config.ip4_rmt5;
  ipRemoto6[0] = configGlobal.config.ip1_rmt6; ipRemoto6[1] =  configGlobal.config.ip2_rmt6; ipRemoto6[2] =  configGlobal.config.ip3_rmt6; ipRemoto6[3] =  configGlobal.config.ip4_rmt6;
  ipRemoto7[0] = configGlobal.config.ip1_rmt7; ipRemoto7[1] =  configGlobal.config.ip2_rmt7; ipRemoto7[2] =  configGlobal.config.ip3_rmt7; ipRemoto7[3] =  configGlobal.config.ip4_rmt7;


  HAL_Delay(1000);
  /*enter_critical();
  gravaConfig();
  exit_critical();
  */

  // Configura os parametros de rede do w5500
  W5500_STM32_begin(configRede);
  estadoLinkEthernet = (wizphy_getphylink() == PHY_LINK_ON) ? 1U : 0U;

  // reduz o timout de rede para evitar perda de pocote ou travametno no caso de ausencia de cliente
  // FIX: esse timeout so protegia parcialmente - o envio ainda rodava dentro da
  // ISR de sync (ver HAL_GPIO_EXTI_Callback), entao mesmo 500us x N sockets
  // inacessiveis prendia a interrupcao por tempo suficiente para atrasar a
  // UART/DMA. Agora que o envio foi movido para o loop principal, o valor
  // pequeno aqui continua bom (evita travar o loop por muito tempo se um
  // cliente cair), mas nao e mais a unica linha de defesa.
  wiz_NetTimeout timeout_rede;
  timeout_rede.retry_cnt = 1;	 // default 8
  timeout_rede.time_100us = 5;	 // default 200

  wizchip_settimeout(&timeout_rede);

  switch (configGlobal.config.n_remotos) {
	case 1:
		 if(W5500_STM32_UDP_begin(8881, UDP_SOCKET1, MODO_UDP_UNI_BROAD) != UDP_SOCKET1){
			  printf("Falha na inicialização do socket UDP");
			  Error_Handler();
		  }
		break;
	case 2:
		  if(W5500_STM32_UDP_begin(8881, UDP_SOCKET1, MODO_UDP_UNI_BROAD) != UDP_SOCKET1){
			  printf("Falha na inicialização do socket UDP");
			  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8882, UDP_SOCKET2, MODO_UDP_UNI_BROAD) != UDP_SOCKET2){
			  printf("Falha na inicialização do socket UDP");
			  Error_Handler();
		  }
		break;
	case 3:
		  if(W5500_STM32_UDP_begin(8881, UDP_SOCKET1, MODO_UDP_UNI_BROAD) != UDP_SOCKET1){
			  printf("Falha na inicialização do socket UDP");
			  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8882, UDP_SOCKET2, MODO_UDP_UNI_BROAD) != UDP_SOCKET2){
		 	  printf("Falha na inicialização do socket UDP");
		 	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET3, MODO_UDP_UNI_BROAD) != UDP_SOCKET3){
		 	  printf("Falha na inicialização do socket UDP");
		 	  Error_Handler();
		  }
		break;
	case 4:
		  if(W5500_STM32_UDP_begin(8881, UDP_SOCKET1, MODO_UDP_UNI_BROAD) != UDP_SOCKET1){
			  printf("Falha na inicialização do socket UDP");
			  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8882, UDP_SOCKET2, MODO_UDP_UNI_BROAD) != UDP_SOCKET2){
		 	  printf("Falha na inicialização do socket UDP");
		 	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET3, MODO_UDP_UNI_BROAD) != UDP_SOCKET3){
		 	  printf("Falha na inicialização do socket UDP");
		 	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET4, MODO_UDP_UNI_BROAD) != UDP_SOCKET4){
		   	  printf("Falha na inicialização do socket UDP");
		   	  Error_Handler();
		  }
		break;
	case 5:
		  if(W5500_STM32_UDP_begin(8881, UDP_SOCKET1, MODO_UDP_UNI_BROAD) != UDP_SOCKET1){
			  printf("Falha na inicialização do socket UDP");
			  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8882, UDP_SOCKET2, MODO_UDP_UNI_BROAD) != UDP_SOCKET2){
		 	  printf("Falha na inicialização do socket UDP");
		 	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET3, MODO_UDP_UNI_BROAD) != UDP_SOCKET3){
		 	  printf("Falha na inicialização do socket UDP");
		 	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET4, MODO_UDP_UNI_BROAD) != UDP_SOCKET4){
		   	  printf("Falha na inicialização do socket UDP");
		   	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET5, MODO_UDP_UNI_BROAD) != UDP_SOCKET5){
		   	  printf("Falha na inicialização do socket UDP");
		   	  Error_Handler();
		  }

		break;
	case 6:
		  if(W5500_STM32_UDP_begin(8881, UDP_SOCKET1, MODO_UDP_UNI_BROAD) != UDP_SOCKET1){
			  printf("Falha na inicialização do socket UDP");
			  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8882, UDP_SOCKET2, MODO_UDP_UNI_BROAD) != UDP_SOCKET2){
		 	  printf("Falha na inicialização do socket UDP");
		 	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET3, MODO_UDP_UNI_BROAD) != UDP_SOCKET3){
		 	  printf("Falha na inicialização do socket UDP");
		 	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET4, MODO_UDP_UNI_BROAD) != UDP_SOCKET4){
		   	  printf("Falha na inicialização do socket UDP");
		   	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET5, MODO_UDP_UNI_BROAD) != UDP_SOCKET5){
		   	  printf("Falha na inicialização do socket UDP");
		   	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET6, MODO_UDP_UNI_BROAD) != UDP_SOCKET6){
			  printf("Falha na inicialização do socket UDP");
			  Error_Handler();
		  }

		break;
	case 7:
		  if(W5500_STM32_UDP_begin(8881, UDP_SOCKET1, MODO_UDP_UNI_BROAD) != UDP_SOCKET1){
			  printf("Falha na inicialização do socket UDP");
			  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8882, UDP_SOCKET2, MODO_UDP_UNI_BROAD) != UDP_SOCKET2){
		 	  printf("Falha na inicialização do socket UDP");
		 	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET3, MODO_UDP_UNI_BROAD) != UDP_SOCKET3){
		 	  printf("Falha na inicialização do socket UDP");
		 	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET4, MODO_UDP_UNI_BROAD) != UDP_SOCKET4){
		   	  printf("Falha na inicialização do socket UDP");
		   	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET5, MODO_UDP_UNI_BROAD) != UDP_SOCKET5){
		   	  printf("Falha na inicialização do socket UDP");
		   	  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET6, MODO_UDP_UNI_BROAD) != UDP_SOCKET6){
			  printf("Falha na inicialização do socket UDP");
			  Error_Handler();
		  }
		  if(W5500_STM32_UDP_begin(8883, UDP_SOCKET7, MODO_UDP_UNI_BROAD) != UDP_SOCKET7){
			  printf("Falha na inicialização do socket UDP");
			  Error_Handler();
		  }
		break;
	default:
		printf("Falha na inicializaçao dos socket. Não foi configurado dispositivo remoto \n");
		break;
}

  /*if(listen(TCP_SERVER_SOCKET) != SOCK_OK){
	  printf("Inicio: Falha na inicializacao da escuta do servidor TCP \n");
  }*/

  HAL_Delay(1000);

  enter_critical();

  if(HAL_UARTEx_ReceiveToIdle_DMA(&huart2, (uint8_t *) bufferRX_SerialCam4[idBufferEscrita].bufferRaw, 27) == HAL_ERROR) {
	  printf("Falha na inicialização da porta UART \n");
	  Error_Handler();
  };
  __HAL_DMA_DISABLE_IT(&hdma_usart2_rx,DMA_IT_HT);

  exit_critical();

  printf("Fim do setup! Inicio do loop. \n");

  /* USER CODE END 2 */

  /* Infinite loop */
  /* USER CODE BEGIN WHILE */
  bufferStatusGeral[sizeof(bufferStatusGeral) - 1] = 0x0;  // Zera o ultimo byte para poder imprimpir como uma string \0
  //__HAL_TIM_ENABLE(&htim10);

  HAL_GPIO_WritePin(LED_STATUS_BOOTING_GPIO_Port, LED_STATUS_BOOTING_Pin, GPIO_PIN_RESET);

  while (1)
  {
	 HAL_IWDG_Refresh(&hiwdg);  // reset wtd

	 // Reseta status do sync devido ao timout do sync
	 if(SYNC_ON && HAL_GetTick() - dt_sync >  TIME_OUT_STATUS_SYNC_MS){
		SYNC_ON = 0;
		contadorSyncLost++;
		HAL_GPIO_WritePin(SYNC_OK_STATUS_GPIO_Port, SYNC_OK_STATUS_Pin, SYNC_ON);
	 }
	 // Reseta status do sync devido ao timout do rx da uart
	 if(HAL_GetTick() - dt_rx > TIME_OUT_STATUS_RX_MS){
		 RX_STATUS = 0;
		 HAL_GPIO_WritePin(LED_RX_UART_OK_GPIO_Port, LED_RX_UART_OK_Pin, GPIO_PIN_RESET);
	 }

	 if(HAL_GetTick() - dt   > TEMPO_REFRESH_MENU_MS){

		 dt = HAL_GetTick();
		 uint32_t linkAtual = (wizphy_getphylink() == PHY_LINK_ON) ? 1U : 0U;
		 if(linkAtual != estadoLinkEthernet){
			 estadoLinkEthernet = linkAtual;
			 if(linkAtual){
				 contadorEthUp++;
			 }else{
				 contadorEthDown++;
			 }
		 }
		 bufferStatusGeral[0] = (RX_STATUS | SYNC_ON << 2);
		 bufferStatusGeral[1] = (uint8_t) HAL_UART_GetError(&huart2);
		 bufferStatusGeral[2] = (uint8_t)HAL_SPI_GetError(&hspi1);
		 // FIX (opcional/aditivo): bytes 3-10, antes nao usados, expõem os novos
		 // contadores de diagnostico para quem ja le esse buffer via TCP (ex.:
		 // Cam4Monitor). Nao mexe nos bytes 0-2 existentes, entao nao deveria
		 // quebrar nenhum parser atual que ja ignore o restante do buffer.
		 uint32_t tmpFalhaEnvio = contadorFalhaEnvioUDP;			// copia local para evitar aviso de 'volatile' no memcpy
		 uint32_t tmpFalhaEnquadramento = contadorFalhaEnquadramentoUART;
		 memcpy(&bufferStatusGeral[3], &tmpFalhaEnvio, sizeof(tmpFalhaEnvio));
		 memcpy(&bufferStatusGeral[7], &tmpFalhaEnquadramento, sizeof(tmpFalhaEnquadramento));

		 SERVER_MENU(bufferStatusGeral, sizeof(bufferStatusGeral), buffetMsgOut, sizeof(buffetMsgOut), TCP_SERVER_SOCKET, PORTA_RX_TCP_SERVER, MODO_TCP_UNI_BROAD);

		 if(HAL_UART_GetError(&huart2) > 0){ // Reinica o processo da UART COM A DMA em caso de erro
			HAL_UART_DMAStop(&huart2);
			HAL_UARTEx_ReceiveToIdle_DMA(&huart2, (uint8_t *) bufferRX_SerialCam4[idBufferEscrita].bufferRaw, SIZEPACKET);
			__HAL_DMA_DISABLE_IT(&hdma_usart2_rx,DMA_IT_HT);
		 }
	 }

    /* USER CODE END WHILE */

    /* USER CODE BEGIN 3 */
  }
  /* USER CODE END 3 */
}

/**
  * @brief System Clock Configuration
  * @retval None
  */
void SystemClock_Config(void)
{
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};

  /** Configure the main internal regulator output voltage
  */
  __HAL_RCC_PWR_CLK_ENABLE();
  __HAL_PWR_VOLTAGESCALING_CONFIG(PWR_REGULATOR_VOLTAGE_SCALE2);

  /** Initializes the RCC Oscillators according to the specified parameters
  * in the RCC_OscInitTypeDef structure.
  */
  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_LSI|RCC_OSCILLATORTYPE_HSE;
  RCC_OscInitStruct.HSEState = RCC_HSE_ON;
  RCC_OscInitStruct.LSIState = RCC_LSI_ON;
  RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;
  RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSE;
  RCC_OscInitStruct.PLL.PLLM = 25;
  RCC_OscInitStruct.PLL.PLLN = 336;
  RCC_OscInitStruct.PLL.PLLP = RCC_PLLP_DIV4;
  RCC_OscInitStruct.PLL.PLLQ = 7;
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)
  {
    Error_Handler();
  }

  /** Initializes the CPU, AHB and APB buses clocks
  */
  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK|RCC_CLOCKTYPE_SYSCLK
                              |RCC_CLOCKTYPE_PCLK1|RCC_CLOCKTYPE_PCLK2;
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_PLLCLK;
  RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;
  RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV2;
  RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV1;

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_2) != HAL_OK)
  {
    Error_Handler();
  }

  /** Enables the Clock Security System
  */
  HAL_RCC_EnableCSS();
}

/**
  * @brief IWDG Initialization Function
  * @param None
  * @retval None
  */
static void MX_IWDG_Init(void)
{

  /* USER CODE BEGIN IWDG_Init 0 */

  /* USER CODE END IWDG_Init 0 */

  /* USER CODE BEGIN IWDG_Init 1 */

  /* USER CODE END IWDG_Init 1 */
  hiwdg.Instance = IWDG;
  hiwdg.Init.Prescaler = IWDG_PRESCALER_64;
  hiwdg.Init.Reload = 2499;
  if (HAL_IWDG_Init(&hiwdg) != HAL_OK)
  {
    Error_Handler();
  }
  /* USER CODE BEGIN IWDG_Init 2 */

  /* USER CODE END IWDG_Init 2 */

}

/**
  * @brief SPI1 Initialization Function
  * @param None
  * @retval None
  */
static void MX_SPI1_Init(void)
{

  /* USER CODE BEGIN SPI1_Init 0 */

  /* USER CODE END SPI1_Init 0 */

  /* USER CODE BEGIN SPI1_Init 1 */

  /* USER CODE END SPI1_Init 1 */
  /* SPI1 parameter configuration*/
  hspi1.Instance = SPI1;
  hspi1.Init.Mode = SPI_MODE_MASTER;
  hspi1.Init.Direction = SPI_DIRECTION_2LINES;
  hspi1.Init.DataSize = SPI_DATASIZE_8BIT;
  hspi1.Init.CLKPolarity = SPI_POLARITY_LOW;
  hspi1.Init.CLKPhase = SPI_PHASE_1EDGE;
  hspi1.Init.NSS = SPI_NSS_SOFT;
  hspi1.Init.BaudRatePrescaler = SPI_BAUDRATEPRESCALER_8;
  hspi1.Init.FirstBit = SPI_FIRSTBIT_MSB;
  hspi1.Init.TIMode = SPI_TIMODE_DISABLE;
  hspi1.Init.CRCCalculation = SPI_CRCCALCULATION_DISABLE;
  hspi1.Init.CRCPolynomial = 10;
  if (HAL_SPI_Init(&hspi1) != HAL_OK)
  {
    Error_Handler();
  }
  /* USER CODE BEGIN SPI1_Init 2 */

  /* USER CODE END SPI1_Init 2 */

}

/**
  * @brief USART2 Initialization Function
  * @param None
  * @retval None
  */
static void MX_USART2_UART_Init(void)
{

  /* USER CODE BEGIN USART2_Init 0 */

  /* USER CODE END USART2_Init 0 */

  /* USER CODE BEGIN USART2_Init 1 */

  /* USER CODE END USART2_Init 1 */
  huart2.Instance = USART2;
  huart2.Init.BaudRate = 115200;
  huart2.Init.WordLength = UART_WORDLENGTH_8B;
  huart2.Init.StopBits = UART_STOPBITS_1;
  huart2.Init.Parity = UART_PARITY_NONE;
  huart2.Init.Mode = UART_MODE_TX_RX;
  huart2.Init.HwFlowCtl = UART_HWCONTROL_NONE;
  huart2.Init.OverSampling = UART_OVERSAMPLING_16;
  if (HAL_UART_Init(&huart2) != HAL_OK)
  {
    Error_Handler();
  }
  /* USER CODE BEGIN USART2_Init 2 */

  /* USER CODE END USART2_Init 2 */

}

/**
  * Enable DMA controller clock
  */
static void MX_DMA_Init(void)
{

  /* DMA controller clock enable */
  __HAL_RCC_DMA1_CLK_ENABLE();

  /* DMA interrupt init */
  /* DMA1_Stream5_IRQn interrupt configuration */
  HAL_NVIC_SetPriority(DMA1_Stream5_IRQn, 0, 0);
  HAL_NVIC_EnableIRQ(DMA1_Stream5_IRQn);

}

/**
  * @brief GPIO Initialization Function
  * @param None
  * @retval None
  */
static void MX_GPIO_Init(void)
{
  GPIO_InitTypeDef GPIO_InitStruct = {0};

  /* GPIO Ports Clock Enable */
  __HAL_RCC_GPIOC_CLK_ENABLE();
  __HAL_RCC_GPIOH_CLK_ENABLE();
  __HAL_RCC_GPIOA_CLK_ENABLE();
  __HAL_RCC_GPIOB_CLK_ENABLE();

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOA, SOFT_RST_ETH_Pin|SOFT_CS_ETH_Pin|LED_STATUS_TX_UDP_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOB, LED_ERROR_HANDLE_Pin|LED_RX_UART_OK_Pin|LED_STATUS_BOOTING_Pin|SYNC_OK_STATUS_Pin, GPIO_PIN_RESET);

  /*Configure GPIO pin : TRIGGER_SYNC_Pin */
  GPIO_InitStruct.Pin = TRIGGER_SYNC_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_IT_FALLING;
  GPIO_InitStruct.Pull = GPIO_PULLUP;
  HAL_GPIO_Init(TRIGGER_SYNC_GPIO_Port, &GPIO_InitStruct);

  /*Configure GPIO pin : BT_LOAD_DEFAULT_Pin */
  GPIO_InitStruct.Pin = BT_LOAD_DEFAULT_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_INPUT;
  GPIO_InitStruct.Pull = GPIO_PULLUP;
  HAL_GPIO_Init(BT_LOAD_DEFAULT_GPIO_Port, &GPIO_InitStruct);

  /*Configure GPIO pins : SOFT_RST_ETH_Pin SOFT_CS_ETH_Pin LED_STATUS_TX_UDP_Pin */
  GPIO_InitStruct.Pin = SOFT_RST_ETH_Pin|SOFT_CS_ETH_Pin|LED_STATUS_TX_UDP_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

  /*Configure GPIO pins : LED_ERROR_HANDLE_Pin LED_RX_UART_OK_Pin LED_STATUS_BOOTING_Pin SYNC_OK_STATUS_Pin */
  GPIO_InitStruct.Pin = LED_ERROR_HANDLE_Pin|LED_RX_UART_OK_Pin|LED_STATUS_BOOTING_Pin|SYNC_OK_STATUS_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  /*Configure GPIO pin : KNOB_A_Pin */
  GPIO_InitStruct.Pin = KNOB_A_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_IT_RISING;
  GPIO_InitStruct.Pull = GPIO_PULLDOWN;
  HAL_GPIO_Init(KNOB_A_GPIO_Port, &GPIO_InitStruct);

  /*Configure GPIO pins : KNOB_B_Pin KNOB_PUSH_Pin */
  GPIO_InitStruct.Pin = KNOB_B_Pin|KNOB_PUSH_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_IT_RISING;
  GPIO_InitStruct.Pull = GPIO_PULLUP;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  /* EXTI interrupt init*/
  // FIX: prioridade rebaixada (1) em relacao a DMA1_Stream5_IRQn (0, a
  // interrupcao de RX da UART da camera). Antes as duas tinham prioridade
  // igual (0,0), entao, uma vez iniciada a ISR do sync, a conclusao da DMA da
  // UART ficava esperando ela terminar - sem chance de preempcao. Com o envio
  // de rede ja fora da ISR de sync isso importa menos, mas manter a UART com
  // prioridade mais alta e defesa extra contra qualquer trabalho futuro que
  // volte a ser colocado aqui.
  HAL_NVIC_SetPriority(EXTI15_10_IRQn, 1, 0);
  HAL_NVIC_EnableIRQ(EXTI15_10_IRQn);

}

/* USER CODE BEGIN 4 */
void HAL_GPIO_EXTI_Callback(uint16_t GPIO_Pin){

	switch (GPIO_Pin) {
		case TRIGGER_SYNC_Pin:
			dt_sync = HAL_GetTick(); // marca o ponto do sync para inciar ausencia do mesmo.
			ultimoSyncTick = dt_sync;
			contadorSyncTotal++;

#ifdef DEBUG_ITM
			ITM_Port32(31) = NORMALIZA_ID_BUF_DW(idBufferEscrita-1, SizeVectorBuffer);
#endif
			//timmerVal = __HAL_TIM_GET_COUNTER(&htim10);
			//__HAL_RCC_TIM10_FORCE_RESET();


			SYNC_ON = 1;
			HAL_GPIO_WritePin(LED_STATUS_TX_UDP_GPIO_Port, LED_STATUS_TX_UDP_Pin, GPIO_PIN_SET);

			CamDataToFreeD((uint8_t *) bufferUdpTX, &bufferRX_SerialCam4[NORMALIZA_ID_BUF_DW(idBufferEscrita-1, SizeVectorBuffer)],
							CAM_ID, &gapLente,
							&mapaST_XY, &mapaST_Z, &mapaST_PAN, &mapaST_T_ROLL,
							VetMapXY, VetMapZ,VetMapaPAN, VetMapaROLL);

#define ENVIA_FREED(status, socket, ip, porta) do { \
	uint32_t retornoBrutoFreeD = W5500_STM32_UDP_Send((socket), (uint8_t *) bufferUdpTX, 29, (ip), (porta)); \
	(status) = retornoBrutoFreeD; \
	ultimoFreeDRetorno = (retornoBrutoFreeD <= INT32_MAX) ? (int32_t)retornoBrutoFreeD : -(int32_t)(UINT32_MAX - retornoBrutoFreeD) - 1; \
	ultimoFreeDSocket = (socket); \
	contadorFreeDTotal++; \
	if((status) == 29) contadorFreeDOk++; else contadorFreeDErro++; \
	ultimoFreeDTick = HAL_GetTick(); \
} while(0)

			switch (configGlobal.config.n_remotos) {
				case 1:
					ENVIA_FREED(stsSocket0, UDP_SOCKET1, ipRemoto1, configGlobal.config.porta_rmt1);
					break;
				case 2:
					ENVIA_FREED(stsSocket0, UDP_SOCKET1, ipRemoto1, configGlobal.config.porta_rmt1);
					ENVIA_FREED(stsSocket1, UDP_SOCKET2, ipRemoto2, configGlobal.config.porta_rmt2);
					break;
				case 3:
					ENVIA_FREED(stsSocket0, UDP_SOCKET1, ipRemoto1, configGlobal.config.porta_rmt1);
					ENVIA_FREED(stsSocket1, UDP_SOCKET2, ipRemoto2, configGlobal.config.porta_rmt2);
					ENVIA_FREED(stsSocket2, UDP_SOCKET3, ipRemoto3, configGlobal.config.porta_rmt3);
					break;
				case 4:
					ENVIA_FREED(stsSocket0, UDP_SOCKET1, ipRemoto1, configGlobal.config.porta_rmt1);
					ENVIA_FREED(stsSocket1, UDP_SOCKET2, ipRemoto2, configGlobal.config.porta_rmt2);
					ENVIA_FREED(stsSocket2, UDP_SOCKET3, ipRemoto3, configGlobal.config.porta_rmt3);
					ENVIA_FREED(stsSocket3, UDP_SOCKET4, ipRemoto4, configGlobal.config.porta_rmt4);
					break;
				case 5:
					ENVIA_FREED(stsSocket0, UDP_SOCKET1, ipRemoto1, configGlobal.config.porta_rmt1);
					ENVIA_FREED(stsSocket1, UDP_SOCKET2, ipRemoto2, configGlobal.config.porta_rmt2);
					ENVIA_FREED(stsSocket2, UDP_SOCKET3, ipRemoto3, configGlobal.config.porta_rmt3);
					ENVIA_FREED(stsSocket3, UDP_SOCKET4, ipRemoto4, configGlobal.config.porta_rmt4);
					ENVIA_FREED(stsSocket4, UDP_SOCKET5, ipRemoto5, configGlobal.config.porta_rmt5);
					break;
				case 6:
					ENVIA_FREED(stsSocket0, UDP_SOCKET1, ipRemoto1, configGlobal.config.porta_rmt1);
					ENVIA_FREED(stsSocket1, UDP_SOCKET2, ipRemoto2, configGlobal.config.porta_rmt2);
					ENVIA_FREED(stsSocket2, UDP_SOCKET3, ipRemoto3, configGlobal.config.porta_rmt3);
					ENVIA_FREED(stsSocket3, UDP_SOCKET4, ipRemoto4, configGlobal.config.porta_rmt4);
					ENVIA_FREED(stsSocket4, UDP_SOCKET5, ipRemoto5, configGlobal.config.porta_rmt5);
					ENVIA_FREED(stsSocket5, UDP_SOCKET6, ipRemoto6, configGlobal.config.porta_rmt6);
					break;
				case 7:
					ENVIA_FREED(stsSocket0, UDP_SOCKET1, ipRemoto1, configGlobal.config.porta_rmt1);
					ENVIA_FREED(stsSocket1, UDP_SOCKET2, ipRemoto2, configGlobal.config.porta_rmt2);
					ENVIA_FREED(stsSocket2, UDP_SOCKET3, ipRemoto3, configGlobal.config.porta_rmt3);
					ENVIA_FREED(stsSocket3, UDP_SOCKET4, ipRemoto4, configGlobal.config.porta_rmt4);
					ENVIA_FREED(stsSocket4, UDP_SOCKET5, ipRemoto5, configGlobal.config.porta_rmt5);
					ENVIA_FREED(stsSocket5, UDP_SOCKET6, ipRemoto6, configGlobal.config.porta_rmt6);
					ENVIA_FREED(stsSocket6, UDP_SOCKET7, ipRemoto7, configGlobal.config.porta_rmt7);
					break;
				default:
					break;
			}

#undef ENVIA_FREED

			if(stsSocket0 != 29 && configGlobal.config.n_remotos >= 1) contadorFalhaEnvioUDP++;
			contadorFreeDDropped = 0; // Direct-send architecture: no queue or double buffer.
			HAL_GPIO_WritePin(LED_STATUS_TX_UDP_GPIO_Port, LED_STATUS_TX_UDP_Pin, GPIO_PIN_RESET);

			HAL_GPIO_WritePin(SYNC_OK_STATUS_GPIO_Port, SYNC_OK_STATUS_Pin, GPIO_PIN_SET);			// SETA O STATUS DE COM SYNC TODA VEZ QUE ENTRA NA INTERRUPCAO
			break;
		case KNOB_A_Pin:
			break;
		case KNOB_B_Pin:
			break;
		case KNOB_PUSH_Pin:
			break;
		default:

			break;
	}
}
void HAL_UART_RxCpltCallback(UART_HandleTypeDef *huart)
{
  HAL_UART_Receive_DMA(huart, (uint8_t*)bufferRX_SerialCam4, SIZEPACKET);
}

void  HAL_UART_RxHalfCpltCallback(UART_HandleTypeDef *huart){

}

volatile uint32_t contadorErroUART = 0;
volatile uint32_t ultimoErroUART = 0;
volatile uint32_t contadorErroORE = 0;
volatile uint32_t contadorErroFE  = 0;
volatile uint32_t contadorErroNE  = 0;
volatile uint32_t contadorErroPE  = 0;
volatile uint32_t contadorRXTotal = 0;
volatile uint32_t contadorRXValido = 0;
volatile uint32_t contadorRXInvalido = 0;
volatile uint32_t ultimoRXTick = 0;
volatile uint32_t ultimoRXOkTick = 0;

void HAL_UART_ErrorCallback(UART_HandleTypeDef *huart)
{
    if (huart->Instance == USART2)
    {
        uint32_t erro = HAL_UART_GetError(huart);

        contadorErroUART++;
        ultimoErroUART = erro;

        if (erro & HAL_UART_ERROR_ORE)
            contadorErroORE++;

        if (erro & HAL_UART_ERROR_FE)
            contadorErroFE++;

        if (erro & HAL_UART_ERROR_NE)
            contadorErroNE++;

        if (erro & HAL_UART_ERROR_PE)
            contadorErroPE++;
    }
}

void HAL_UARTEx_RxEventCallback(UART_HandleTypeDef *huart, uint16_t Size){
	if(huart->Instance == USART2){
		dt_rx = HAL_GetTick();		// marca o ponto da leitura para inicar ausencia de rx

		contadorRXTotal++;
		ultimoRXTick = dt_rx;

		RX_STATUS = 1;
#ifdef DEBUG_ITM
		ITM_Port32(30) = NORMALIZA_ID_BUF_UP(idBufferEscrita + 1,SizeVectorBuffer);
#endif
		if(Size == SIZEPACKET){			// Recepcao completa, 27 bytes
			if(bufferRX_SerialCam4[idBufferEscrita].dadosSeprados.marcador1 == MarcadorInicioPac){
				contadorRXValido++;
				ultimoRXOkTick = dt_rx;

				RX_STATUS = 3;
				idBufferEscrita = NORMALIZA_ID_BUF_UP(idBufferEscrita + 1,SizeVectorBuffer);
				HAL_GPIO_WritePin(LED_RX_UART_OK_GPIO_Port, LED_RX_UART_OK_Pin, GPIO_PIN_SET);
#ifdef WTD_RESET
				HAL_IWDG_Refresh(&hiwdg);   // reset do wtd caso a leitura tenha ocorrido de maneira correta.
#endif
			}else{
				// Pacote recebido com o tamanho certo mas marcador invalido:
				// provavel ruido/desalinhamento na UART. Nao avancamos
				// idBufferEscrita (pacote e descartado) e contamos a ocorrencia
				// para dar visibilidade do problema (antes isso era invisivel).
				contadorRXInvalido++;
				contadorFalhaEnquadramentoUART++;
			}
		}else{
			contadorRXInvalido++;
		}
		HAL_UARTEx_ReceiveToIdle_DMA(&huart2, (uint8_t *) bufferRX_SerialCam4[idBufferEscrita].bufferRaw, SIZEPACKET);
		__HAL_DMA_DISABLE_IT(&hdma_usart2_rx,DMA_IT_HT);
	}
}


// Funcao write para printf via debug
int _write(int file, char *ptr, int len)
{
	int DataIdx;
#ifndef DEBUG_USB
	for (DataIdx = 0; DataIdx < len; DataIdx++)
	{
		ITM_SendChar(*ptr++);
	}
#else
	CDC_Transmit_FS(ptr, len);
#endif
	return len;
}

void statusBooting(void){
	HAL_GPIO_WritePin(LED_RX_UART_OK_GPIO_Port, LED_RX_UART_OK_Pin, GPIO_PIN_RESET);
	HAL_GPIO_WritePin(LED_ERROR_HANDLE_GPIO_Port, LED_ERROR_HANDLE_Pin, GPIO_PIN_RESET);
	HAL_GPIO_WritePin(LED_STATUS_BOOTING_GPIO_Port, LED_STATUS_BOOTING_Pin, GPIO_PIN_SET);
}

/* USER CODE END 4 */

/**
  * @brief  This function is executed in case of error occurrence.
  * @retval None
  */
void Error_Handler(void)
{
  /* USER CODE BEGIN Error_Handler_Debug */
  /* User can add his own implementation to report the HAL error return state */
  __disable_irq();
  HAL_GPIO_WritePin(LED_RX_UART_OK_GPIO_Port, LED_RX_UART_OK_Pin, GPIO_PIN_RESET);
  HAL_GPIO_WritePin(SYNC_OK_STATUS_GPIO_Port, SYNC_OK_STATUS_Pin, GPIO_PIN_RESET);
  while (1)  {
	  HAL_GPIO_TogglePin(LED_ERROR_HANDLE_GPIO_Port, LED_ERROR_HANDLE_Pin);
  }
  /* USER CODE END Error_Handler_Debug */
}

#ifdef  USE_FULL_ASSERT
/**
  * @brief  Reports the name of the source file and the source line number
  *         where the assert_param error has occurred.
  * @param  file: pointer to the source file name
  * @param  line: assert_param error line source number
  * @retval None
  */
void assert_failed(uint8_t *file, uint32_t line)
{
  /* USER CODE BEGIN 6 */
  /* User can add his own implementation to report the file name and line number,
     ex: printf("Wrong parameters value: file %s on line %d\r\n", file, line) */
  /* USER CODE END 6 */
}
#endif /* USE_FULL_ASSERT */
