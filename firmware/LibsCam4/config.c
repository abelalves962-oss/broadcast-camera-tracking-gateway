/*
 * config.c
 *
 *  Created on: 15 de jul de 2022
 *      Author: danie
 */

#include "config.h"
#include "main.h"

extern volatile uint32_t contadorErroUART;
extern volatile uint32_t ultimoErroUART;
extern volatile uint32_t contadorErroORE;
extern volatile uint32_t contadorErroFE;
extern volatile uint32_t contadorErroNE;
extern volatile uint32_t contadorErroPE;
extern volatile uint32_t contadorFalhaEnquadramentoUART;

extern volatile uint32_t contadorRXTotal;
extern volatile uint32_t contadorRXValido;
extern volatile uint32_t contadorRXInvalido;
extern volatile uint32_t ultimoRXTick;
extern volatile uint32_t ultimoRXOkTick;
extern volatile uint32_t contadorSyncTotal;
extern volatile uint32_t contadorSyncLost;
extern volatile uint32_t ultimoSyncTick;
extern volatile uint32_t contadorFreeDTotal;
extern volatile uint32_t contadorFreeDOk;
extern volatile uint32_t contadorFreeDErro;
extern volatile uint32_t contadorFreeDDropped;
extern volatile uint32_t ultimoFreeDTick;
extern volatile int32_t ultimoFreeDRetorno;
extern volatile uint8_t ultimoFreeDSocket;
extern volatile uint32_t contadorEthDown;
extern volatile uint32_t contadorEthUp;
extern volatile uint32_t estadoLinkEthernet;

extern volatile uint32_t tcpAcceptCount;
extern volatile uint32_t tcpRxOkCount;
extern volatile uint32_t tcpRxErrCount;
extern volatile uint32_t tcpSendCallCount;
extern volatile uint32_t tcpSendRetOkCount;
extern volatile uint32_t tcpSendRetErrCount;
extern volatile uint32_t tcpSendOkCount;
extern volatile uint32_t tcpTimeoutCount;
extern volatile uint32_t tcpCloseWaitCount;
extern volatile uint32_t tcpDisconCount;
extern volatile uint32_t tcpReopenCount;
extern volatile uint8_t tcpLastSr;
extern volatile uint8_t tcpLastIr;
extern volatile int32_t tcpLastRecv;
extern volatile int32_t tcpLastSend;
extern volatile uint16_t tcpLastTxFsr;
extern volatile uint8_t tcpPreviousSocketState;
extern volatile uint8_t tcpPreviousIr;
extern volatile uint8_t tcpSocketEverOpened;
extern volatile uint32_t tcpListenCount;
extern volatile uint32_t tcpClosedToListenMs;
extern volatile uint32_t tcpMaxClosedToListenMs;
extern volatile uint32_t tcpClosedObservedTick;
extern volatile uint8_t tcpClosedTimingActive;

/* Prototipos de funcoes utilizadas neste modulo */
void Error_Handler(void);
void statusBooting(void);
void printConfig(char *vetMsg);
void printDiagnostico(char *vetMsg);

extern configTrack configGlobal; 			// Variavel que carrega as configuracoes em uso. Deve ser declarada na main

extern volatile uint16_t bootPage0Hdr;
extern volatile uint16_t bootPage1Hdr;
extern volatile uint16_t afterEeInitPage0Hdr;
extern volatile uint16_t afterEeInitPage1Hdr;
extern volatile uint16_t bootBtLoadDefault;

volatile uint16_t saveLastIndex = 0;
volatile int16_t saveFirstError = -1;
volatile uint16_t saveLastStatus = HAL_OK;
volatile uint16_t saveSuccessCount = 0;
volatile uint16_t saveFailCount = 0;
volatile uint16_t lastWriteVa = 0;
volatile uint16_t lastWriteValue = 0;
volatile uint16_t saveReadbackStatus[7] = {0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF};
volatile uint16_t saveReadbackValue[7] = {0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF};

static uint16_t diagWriteVariable(uint16_t virtAddress, uint16_t data)
{
	uint16_t status;

	lastWriteVa = virtAddress;
	lastWriteValue = data;
	saveLastIndex = virtAddress;
	status = EE_WriteVariable(virtAddress, data);
	saveLastStatus = status;
	if(status == HAL_OK){
		saveSuccessCount++;
	}else{
		saveFailCount++;
		if(saveFirstError < 0)
			saveFirstError = (int16_t)virtAddress;
	}

	return status;
}

static void printEepromDiag(char *vetMsg)
{
	static const uint16_t readbackVa[7] = {0, 64, 65, 66, 67, 68, 69};
	uint16_t verValue = 0xFFFF;
	uint16_t verStatus = EE_ReadVariable(0, &verValue);
	int16_t firstReadFail = -1;
	uint16_t tempValue;

	for(uint16_t i = 0; i < 88; i++){
		if(EE_ReadVariable(i, &tempValue) != HAL_OK){
			firstReadFail = (int16_t)i;
			break;
		}
	}

	sprintf(vetMsg,
		"EEPROM_DIAG\n"
		"PAGE0_HDR=%04X\nPAGE1_HDR=%04X\n"
		"READ_PAGE=%08X\nWRITE_PAGE=%08X\n"
		"VER_STATUS=%u\nVER_VALUE=%u\nFIRST_READ_FAIL=%d\n"
		"BT_LOAD_DEFAULT=%u\nFLASH_SR=%08lX\nFLASH_CR=%08lX\nFLASH_ERROR=%08lX\n"
		"SAVE_LAST_INDEX=%u\nSAVE_FIRST_ERROR=%d\nSAVE_LAST_STATUS=%u\n"
		"SAVE_SUCCESS_COUNT=%u\nSAVE_FAIL_COUNT=%u\n"
		"LAST_WRITE_VA=%u\nLAST_WRITE_VALUE=%u\n"
		"RB%u=%u,%u\nRB%u=%u,%u\nRB%u=%u,%u\nRB%u=%u,%u\n"
		"RB%u=%u,%u\nRB%u=%u,%u\nRB%u=%u,%u\n"
		"BOOT_PAGE0_HDR=%04X\nBOOT_PAGE1_HDR=%04X\n"
		"AFTER_EEINIT_PAGE0_HDR=%04X\nAFTER_EEINIT_PAGE1_HDR=%04X\n"
		"BOOT_BT_LOAD_DEFAULT=%u\n",
		(unsigned int)(*(__IO uint16_t*)PAGE0_BASE_ADDRESS),
		(unsigned int)(*(__IO uint16_t*)PAGE1_BASE_ADDRESS),
		(unsigned int)EE_DiagFindValidPage(READ_FROM_VALID_PAGE),
		(unsigned int)EE_DiagFindValidPage(WRITE_IN_VALID_PAGE),
		(unsigned int)verStatus, (unsigned int)verValue, (int)firstReadFail,
		(unsigned int)HAL_GPIO_ReadPin(BT_LOAD_DEFAULT_GPIO_Port, BT_LOAD_DEFAULT_Pin),
		(unsigned long)FLASH->SR, (unsigned long)FLASH->CR, (unsigned long)HAL_FLASH_GetError(),
		(unsigned int)saveLastIndex, (int)saveFirstError, (unsigned int)saveLastStatus,
		(unsigned int)saveSuccessCount, (unsigned int)saveFailCount,
		(unsigned int)lastWriteVa, (unsigned int)lastWriteValue,
		(unsigned int)readbackVa[0], (unsigned int)saveReadbackStatus[0], (unsigned int)saveReadbackValue[0],
		(unsigned int)readbackVa[1], (unsigned int)saveReadbackStatus[1], (unsigned int)saveReadbackValue[1],
		(unsigned int)readbackVa[2], (unsigned int)saveReadbackStatus[2], (unsigned int)saveReadbackValue[2],
		(unsigned int)readbackVa[3], (unsigned int)saveReadbackStatus[3], (unsigned int)saveReadbackValue[3],
		(unsigned int)readbackVa[4], (unsigned int)saveReadbackStatus[4], (unsigned int)saveReadbackValue[4],
		(unsigned int)readbackVa[5], (unsigned int)saveReadbackStatus[5], (unsigned int)saveReadbackValue[5],
		(unsigned int)readbackVa[6], (unsigned int)saveReadbackStatus[6], (unsigned int)saveReadbackValue[6],
		(unsigned int)bootPage0Hdr, (unsigned int)bootPage1Hdr,
		(unsigned int)afterEeInitPage0Hdr, (unsigned int)afterEeInitPage1Hdr,
		(unsigned int)bootBtLoadDefault);
}

extern uint8_t * buffetStatusGeral;

uint8_t configInit(uint16_t * virtualAddr, uint16_t n_addrs){
    /* Verify Flash is unlocked */
    if(READ_BIT(FLASH->CR, FLASH_CR_LOCK) != RESET)
    {
    	/* Unlock the Flash Program Erase controller */
    	HAL_FLASH_Unlock();
    }

	bootPage0Hdr = (*(__IO uint16_t*)PAGE0_BASE_ADDRESS);
	bootPage1Hdr = (*(__IO uint16_t*)PAGE1_BASE_ADDRESS);

	/* EEPROM Init */
	uint16_t eeInitStatus = EE_Init();

	afterEeInitPage0Hdr = (*(__IO uint16_t*)PAGE0_BASE_ADDRESS);
	afterEeInitPage1Hdr = (*(__IO uint16_t*)PAGE1_BASE_ADDRESS);

	if( eeInitStatus != EE_OK)
	{
		Error_Handler();
	}

	// Inicia os enderecos virtuais da eeprom
	for(uint16_t i = 0; i < n_addrs; i ++){
		virtualAddr[i] = i;				 // Primerio endereço virtul não pode ser zero
	}

	loadDefault();						// Carrega a variavel de operacao com a configuracao default, salva no codigo

	return HAL_OK;
}

void loadDefault(void){   // CARREGA A CONFIGURAÇÃO, SALVA EM CODIGO, PARA A VARIAVEL DE OPERACAO

	///******* CONFIGURAÇÃO DE REDE DA PLACA */////////////

	configGlobal.config.ver = VER_DEFAULT;

	configGlobal.config.mac1 = 0x02;
	configGlobal.config.mac2 = 0x00;
	configGlobal.config.mac3 = 0x00;
	configGlobal.config.mac4 = 0x00;
	configGlobal.config.mac5 = 0x00;
	configGlobal.config.mac6 = 0x10;

	configGlobal.config.ip1 = 192;
	configGlobal.config.ip2 = 0;
	configGlobal.config.ip3 = 2;
	configGlobal.config.ip4 = 10;

	configGlobal.config.mask1 = 255;
	configGlobal.config.mask2 = 255;
	configGlobal.config.mask3 = 224;
	configGlobal.config.mask4 = 0;

	configGlobal.config.gw1 = 0;
	configGlobal.config.gw2 = 0;
	configGlobal.config.gw3 = 0;
	configGlobal.config.gw4 = 0;

	configGlobal.config.dns1 = 0;
	configGlobal.config.dns2 = 0;
	configGlobal.config.dns3 = 0;
	configGlobal.config.dns4 = 0;

	configGlobal.config.dhcp_static = NETINFO_STATIC;

	///-----------CONFIGURACAO DISPOSITIVOS REMOTOS -------------------/////
	// Public example configuration using RFC 5737 documentation addresses.
	// Replace these values in a private deployment configuration.

	configGlobal.config.n_remotos = 1;

	configGlobal.config.ip1_rmt1 = 192;
	configGlobal.config.ip2_rmt1 = 0;
	configGlobal.config.ip3_rmt1 = 2;
	configGlobal.config.ip4_rmt1 = 20;
	configGlobal.config.porta_rmt1 = 1111;

	configGlobal.config.ip1_rmt2 = 192;
	configGlobal.config.ip2_rmt2 = 0;
	configGlobal.config.ip3_rmt2 = 2;
	configGlobal.config.ip4_rmt2 = 21;
	configGlobal.config.porta_rmt2 = 1111;

	configGlobal.config.ip1_rmt3 = 192;
	configGlobal.config.ip2_rmt3 = 0;
	configGlobal.config.ip3_rmt3 = 2;
	configGlobal.config.ip4_rmt3 = 22;
	configGlobal.config.porta_rmt3 = 1111;

	configGlobal.config.ip1_rmt4 = 192;
	configGlobal.config.ip2_rmt4 = 0;
	configGlobal.config.ip3_rmt4 = 2;
	configGlobal.config.ip4_rmt4 = 23;
	configGlobal.config.porta_rmt4 = 1111;

	configGlobal.config.ip1_rmt5= 192;
	configGlobal.config.ip2_rmt5 = 0;
	configGlobal.config.ip3_rmt5 = 2;
	configGlobal.config.ip4_rmt5 = 24;
	configGlobal.config.porta_rmt5 = 1111;

	configGlobal.config.ip1_rmt6 = 192;
	configGlobal.config.ip2_rmt6 = 0;
	configGlobal.config.ip3_rmt6 = 2;
	configGlobal.config.ip4_rmt6 = 25;
	configGlobal.config.porta_rmt6 = 1111;

	configGlobal.config.ip1_rmt7 = 192;
	configGlobal.config.ip2_rmt7 = 0;
	configGlobal.config.ip3_rmt7 = 2;
	configGlobal.config.ip4_rmt7 = 26;
	configGlobal.config.porta_rmt7 = 1111;

///------------- CONFIGURACAO DE EIXOS ------------------------------//
	configGlobal.config.invEixos = 0;
	configGlobal.config.ordem_XYZ = YXZ;
	configGlobal.config.ordem_PTR = PTR;
// ------------  CONFIGURAÇÃO DA COMPENSACAO DE EIXOS  -------------//

	configGlobal.config.configComp = COMP_XY | COMP_Z | COMP_ROT | COMP_ROL | COMP_MODO_ROL_DINAM;

// ------------  Offsets para reposicionamento dos eixos no freed  -------------//

	configGlobal.config.offset_freed.offsetData.x = 0.0;
	configGlobal.config.offset_freed.offsetData.y = 0.0;
	configGlobal.config.offset_freed.offsetData.z = 0.0;
	configGlobal.config.offset_freed.offsetData.pan = 0.0;
	configGlobal.config.offset_freed.offsetData.tilt = 0.0;
	configGlobal.config.offset_freed.offsetData.roll = 0.0;
}

uint8_t loadConfig(void){

	uint16_t VarDataTmp;
	if(EE_ReadVariable(0, &VarDataTmp) != HAL_OK)
	{
	  printf("Endereco virtual nao encontrado! Falha na leitura dos parametros por loadConfig. \n");
	}else{
		if(VarDataTmp != configGlobal.config.ver){				// cASO A VESRAO DA EEPROM SERJA DIFERENTE DA DEFAULT, CARREGA A DA EEPROM.
			for(uint16_t i = 1; i < sizeof(configGlobal.vetConfig) / 2; i ++){
				if(EE_ReadVariable(i, &configGlobal.vetConfig[i]) != HAL_OK){
					printf("Falha na leitura do preset pela eeprom! Carregado preset default \n");
					loadDefault();
					break;
				}
			}
			configGlobal.config.ver = VarDataTmp;
		}
	}
}

uint8_t gravaConfig(void){

    /* Verify Flash is unlocked */
    if(READ_BIT(FLASH->CR, FLASH_CR_LOCK) != RESET)
    {
    	/* Unlock the Flash Program Erase controller */
    	HAL_FLASH_Unlock();
    }

	configGlobal.config.ver++; 		// Incrementa a versao para ser gravada uma nova da eeprom

	if(configGlobal.config.ver == VER_DEFAULT){	// versao 0 é a default, ao gravar na eeprom sempre salva uma diferente da default.
		configGlobal.config.ver++;
	}
	saveLastIndex = 0;
	saveFirstError = -1;
	saveLastStatus = HAL_OK;
	saveSuccessCount = 0;
	saveFailCount = 0;
	lastWriteVa = 0;
	lastWriteValue = 0;
	uint16_t i = 0;
	for(i ; i < sizeof(configGlobal.vetConfig) / 2; i ++){			// Copia do valor carregado na variavel de uso para a eeprom.
		if(diagWriteVariable(i, configGlobal.vetConfig[i]) != HAL_OK){
			printf("Falha da gravacao da configuraca. Endereco: %u!!! \n", i);
			for(int j = 1; j < 5; j ++){
				if(diagWriteVariable(i, configGlobal.vetConfig[i]) != HAL_OK){
					printf("Falha tentativa %u do endereco %u! \n", j + 1,  i);
				}else{
					printf("Gravado na tentativa %u \n", j + 1);
					break;
				}
			}
		}else{
			printf("Gravada variavel no endereco %u da eeprom!\n", i);
		}
	}
	static const uint16_t readbackVa[7] = {0, 64, 65, 66, 67, 68, 69};
	for(uint16_t j = 0; j < 7; j++){
		uint16_t value = 0xFFFF;
		saveReadbackStatus[j] = EE_ReadVariable(readbackVa[j], &value);
		saveReadbackValue[j] = value;
	}
	return i;
}

void SERVER_MENU(uint8_t * buffetStatusGeral, uint16_t sizeBufferStatusGeral, uint8_t * bufferMgsOut, uint16_t sizeBufferMsgOut, uint8_t socketNumber, uint16_t PORTA, uint16_t flag){

	/* Reset data buffer de saída */
	memset(bufferMgsOut, 0x00, sizeBufferMsgOut);

	int32_t statusRecTCP = -1;
	uint16_t lastPose = 0, sizeMsgOut = 0;
	uint8_t socketState = getSn_SR(socketNumber);
	uint8_t socketIr = getSn_IR(socketNumber);
	tcpLastSr = socketState;
	tcpLastIr = socketIr;
	tcpLastTxFsr = getSn_TX_FSR(socketNumber);
	uint8_t previousSocketState = tcpPreviousSocketState;
	uint32_t observedTick = HAL_GetTick();

	/* Observar eventos sem limpar Sn_IR. A deteccao por borda evita contar
	 * repetidamente um bit que o driver atual ainda nao consumiu. */
	if((socketIr & Sn_IR_SENDOK) != 0U && (tcpPreviousIr & Sn_IR_SENDOK) == 0U)
		tcpSendOkCount++;
	if((socketIr & Sn_IR_TIMEOUT) != 0U && (tcpPreviousIr & Sn_IR_TIMEOUT) == 0U)
		tcpTimeoutCount++;
	tcpPreviousIr = socketIr;

	if(socketState == SOCK_ESTABLISHED && previousSocketState != SOCK_ESTABLISHED)
		tcpAcceptCount++;
	if(socketState == SOCK_CLOSE_WAIT && previousSocketState != SOCK_CLOSE_WAIT)
		tcpCloseWaitCount++;
	tcpPreviousSocketState = socketState;
	if(socketState == SOCK_CLOSED && previousSocketState != SOCK_CLOSED){
		tcpClosedObservedTick = observedTick;
		tcpClosedTimingActive = 1U;
	}

	switch (socketState) {
		case SOCK_CLOSED:
			//printf("SOCK_CLOSED 1 ");
			if(previousSocketState != SOCK_CLOSED && tcpSocketEverOpened != 0U)
				tcpReopenCount++;
			tcpSocketEverOpened = 1U;
			W5500_STM32_TCP_begin(PORTA, socketNumber, MODO_TCP_UNI_BROAD);
			/* socket() deixa o W5500 em INIT; abrir o LISTEN no mesmo ciclo
			 * elimina uma chamada adicional de SERVER_MENU (~16 ms). */
			if(getSn_SR(socketNumber) == SOCK_INIT)
				listen(socketNumber);
			{
				uint8_t stateAfterOpen = getSn_SR(socketNumber);
				if(stateAfterOpen == SOCK_LISTEN && tcpLastSr != SOCK_LISTEN){
					tcpListenCount++;
					if(tcpClosedTimingActive != 0U){
						tcpClosedToListenMs = HAL_GetTick() - tcpClosedObservedTick;
						if(tcpClosedToListenMs > tcpMaxClosedToListenMs)
							tcpMaxClosedToListenMs = tcpClosedToListenMs;
						tcpClosedTimingActive = 0U;
					}
				}
			}
			//printf("SOCK_CLOSED 2\n");
			break;
		case SOCK_INIT:
			//printf("SOCK_INIT 1 ");
			listen(socketNumber);
			if(getSn_SR(socketNumber) == SOCK_LISTEN && tcpLastSr != SOCK_LISTEN){
				tcpListenCount++;
				if(tcpClosedTimingActive != 0U){
					tcpClosedToListenMs = HAL_GetTick() - tcpClosedObservedTick;
					if(tcpClosedToListenMs > tcpMaxClosedToListenMs)
						tcpMaxClosedToListenMs = tcpClosedToListenMs;
					tcpClosedTimingActive = 0U;
				}
			}
			//printf("SOCK_INIT 2\n");
			break;
		case SOCK_SYNRECV:
			//printf("SOCK_SYNRECV \n");
			break;
		case SOCK_ESTABLISHED:
			//printf("SOCK_ESTABLISHED 1 ");
			if(getSn_RX_RSR(socketNumber) > 0){

				/* Deixa 1 byte reservado para o terminador '\0' */
				statusRecTCP = recv(socketNumber,
									bufferMgsOut,
									sizeBufferMsgOut - 1);
				tcpLastRecv = statusRecTCP;
				if(statusRecTCP > 0)
					tcpRxOkCount++;
				else if(statusRecTCP < 0)
					tcpRxErrCount++;

				if(statusRecTCP > 0){

					/* recv() entrega bytes, nao uma string terminada */
					bufferMgsOut[statusRecTCP] = '\0';

					printf("Mensagem recebida: %s \n", bufferMgsOut);

					sizeMsgOut = 0;

					check_menu(buffetStatusGeral,
							   bufferMgsOut,
							   &sizeMsgOut);

					/* Protecao antes do envio */
					if(sizeMsgOut > 0 && sizeMsgOut <= sizeBufferMsgOut){
						tcpSendCallCount++;
						tcpLastSend = send(socketNumber,
								 bufferMgsOut,
								 sizeMsgOut);
						if(tcpLastSend > 0)
							tcpSendRetOkCount++;
						else if(tcpLastSend < 0)
							tcpSendRetErrCount++;
					}
				}
			}
			//printf("SOCK_ESTABLISHED 2 \n");

			break;
		case SOCKERR_SOCKINIT: case SOCK_CLOSE_WAIT: case SOCK_FIN_WAIT: case SOCK_FATAL:
			//printf("SOCKERR_SOCKINIT 1 ");
			tcpDisconCount++;
			disconnect(socketNumber);
			//printf("SOCKERR_SOCKINIT 2 \n");
			break;
		default:
			break;
	}
}

void check_menu(char * bufferStatusGeral, char * vetMsg, uint16_t * sizeOut){

	if(memcmp(vetMsg, "ip", strlen("ip")) == 0){
		switch (vetMsg[2]) {
			case ':':
				alteraIP(vetMsg, &configGlobal.config.ip1, "IP");
				break;
			case '?':
				printIP(vetMsg, &configGlobal.config.ip1, "IP");
				break;
			case '1':
				if(vetMsg[strlen("ipX")] == ':'){
					alteraIP(vetMsg, &configGlobal.config.ip1_rmt1, "IP CLIENTE 1");
				}else{
					printIP(vetMsg, &configGlobal.config.ip1_rmt1, "IP CLIENTE 1");
				}
				break;
			case '2':
				if(vetMsg[strlen("ipX")] == ':'){
					alteraIP(vetMsg, &configGlobal.config.ip1_rmt2, "IP CLIENTE 2");
				}else{
					printIP(vetMsg, &configGlobal.config.ip1_rmt2, "IP CLIENTE 2");
				}
				break;
			case '3':
				if(vetMsg[strlen("ipX")] == ':'){
					alteraIP(vetMsg, &configGlobal.config.ip1_rmt3, "IP CLIENTE 3");
				}else{
					printIP(vetMsg, &configGlobal.config.ip1_rmt3, "IP CLIENTE 3");
				}
				break;
			case '4':
				if(vetMsg[strlen("ipX")] == ':'){
					alteraIP(vetMsg, &configGlobal.config.ip1_rmt4, "IP CLIENTE 4");
				}else{
					printIP(vetMsg, &configGlobal.config.ip1_rmt4, "IP CLIENTE 4");
				}
				break;
			case '5':
				if(vetMsg[strlen("ipX")] == ':'){
					alteraIP(vetMsg, &configGlobal.config.ip1_rmt5, "IP CLIENTE 5");
				}else{
					printIP(vetMsg, &configGlobal.config.ip1_rmt5, "IP CLIENTE 5");
				}
				break;
			case '6':
				if(vetMsg[strlen("ipX")] == ':'){
					alteraIP(vetMsg, &configGlobal.config.ip1_rmt6, "IP CLIENTE 6");
				}else{
					printIP(vetMsg, &configGlobal.config.ip1_rmt6, "IP CLIENTE 6");
				}
				break;
			case '7':
				if(vetMsg[strlen("ipX")] == ':'){
					alteraIP(vetMsg, &configGlobal.config.ip1_rmt7, "IP CLIENTE 7");
				}else{
					printIP(vetMsg, &configGlobal.config.ip1_rmt7, "IP CLIENTE 7");
				}
				break;
			default:
				sprintf(vetMsg, "? Comando nao encontrado, verifique a sintaxe do mesmo ?\n");
				break;
		}
	}else if(memcmp(vetMsg, "porta", strlen("porta")) == 0){
		switch (vetMsg[strlen("porta")]) {
			case '1':
				if(vetMsg[strlen("portaX")] == ':'){
					alteraParametroNumerico(vetMsg, &configGlobal.config.porta_rmt1, "Porta cliente 1");
				}else{
					printParametroNumerico(vetMsg, configGlobal.config.porta_rmt1, "Porta cliente 1");
				}
			break;
			case '2':
				if(vetMsg[strlen("portaX")] == ':'){
					alteraParametroNumerico(vetMsg, &configGlobal.config.porta_rmt2, "Porta cliente 2");
				}else{
					printParametroNumerico(vetMsg, configGlobal.config.porta_rmt2, "Porta cliente 2");
				}
			break;
			case '3':
				if(vetMsg[strlen("portaX")] == ':'){
					alteraParametroNumerico(vetMsg, &configGlobal.config.porta_rmt3, "Porta cliente 3");
				}else{
					printParametroNumerico(vetMsg, configGlobal.config.porta_rmt3, "Porta cliente 3");
				}
			break;
			case '4':
				if(vetMsg[strlen("portaX")] == ':'){
					alteraParametroNumerico(vetMsg, &configGlobal.config.porta_rmt4, "Porta cliente 4");
				}else{
					printParametroNumerico(vetMsg, configGlobal.config.porta_rmt4, "Porta cliente 4");
				}
			break;
			case '5':
				if(vetMsg[strlen("portaX")] == ':'){
					alteraParametroNumerico(vetMsg, &configGlobal.config.porta_rmt5, "Porta cliente 5");
				}else{
					printParametroNumerico(vetMsg, configGlobal.config.porta_rmt5, "Porta cliente 5");
				}
			break;
			case '6':
				if(vetMsg[strlen("portaX")] == ':'){
					alteraParametroNumerico(vetMsg, &configGlobal.config.porta_rmt6, "Porta cliente 6");
				}else{
					printParametroNumerico(vetMsg, configGlobal.config.porta_rmt6, "Porta cliente 6");
				}
			break;
			case '7':
				if(vetMsg[strlen("portaX")] == ':'){
					alteraParametroNumerico(vetMsg, &configGlobal.config.porta_rmt7, "Porta cliente 7");
				}else{
					printParametroNumerico(vetMsg, configGlobal.config.porta_rmt7, "Porta cliente 7");
				}
			break;
		}
	}else if(memcmp(vetMsg, "sn", strlen("sn")) == 0){
		if(vetMsg[strlen("sn")] == ':'){
			alteraIP(vetMsg, &configGlobal.config.mask1, "SUBNET");
		}else{
			printIP(vetMsg, &configGlobal.config.mask1, "SUBNET");
		}
	}else if(memcmp(vetMsg, "gw", strlen("gw")) == 0){
		if(vetMsg[strlen("gw")] == ':'){
			alteraIP(vetMsg, &configGlobal.config.gw1, "GATEWAY");
		}else{
			printIP(vetMsg, &configGlobal.config.gw1, "GATEWAY");;
		}
	}else if(memcmp(vetMsg, "dns", strlen("dns")) == 0){
		if(vetMsg[strlen("dns")] == ':'){
			alteraIP(vetMsg, &configGlobal.config.dns1, "DNS");
		}else{
			printIP(vetMsg, &configGlobal.config.dns1, "DNS");
		}

	}else if(memcmp(vetMsg, "dhcp", strlen("dhcp")) == 0){

		if(vetMsg[strlen("dhcp")] == ':'){
			char * tempMsg = (strrchr(vetMsg, ':') + 1);
			if(strcmp(tempMsg, "on") == 0 || strcmp(tempMsg, " on") == 0){
				configGlobal.config.dhcp_static = 0;
				sprintf(vetMsg, "? Alterada a configuracao de rede para DHCP ON ?\n");
			}else if(strcmp(tempMsg, "off") == 0 || strcmp(tempMsg, " off") == 0){
				configGlobal.config.dhcp_static = 1;
				sprintf(vetMsg, "? Alterada a configuracao de rede para DHCP OFF ?\n");
			}else{
				sprintf(vetMsg, "? Comando nao identificado, verifique a sitaxe do mesmo ?\n");
			}
		}else{
			printStatusDHCP(vetMsg, "Status DHCP");
		}
	}else if(memcmp(vetMsg, "salva", strlen("salva")) == 0){
		if(vetMsg[strlen("salva")] == ':'){
			uint16_t tempSalvos = gravaConfig();
			if(tempSalvos == sizeof(configGlobal.vetByte) / 2){
				sprintf(vetMsg,"? Gravada configuracao na eeprom. Versao nova: %u. Dados salvos: %u / %u?\n", configGlobal.config.ver, tempSalvos, sizeof(configGlobal.vetByte) / 2);
			}else{
				sprintf(vetMsg,"? Falha ao gravar a versao %u. Parametros salvos com suscesso %u / %u?\n", configGlobal.config.ver, tempSalvos, sizeof(configGlobal.vetByte) / 2);
			}
		}else{
			sprintf(vetMsg,"? Mensagem nao identificada. Verifique a sintaxe. ?\n");
		}
	}else if(memcmp(vetMsg, "clientes", strlen("clientes")) == 0){
		if(vetMsg[strlen("clientes")] == ':'){
			uint16_t remotos = atoi((strrchr(vetMsg, ':') + 1));
			if(remotos < 8){
				configGlobal.config.n_remotos = remotos;
				sprintf(vetMsg, "? Alterado o numero de redeners para: %u ?\n", configGlobal.config.n_remotos);
			}else{
				sprintf(vetMsg,"? Comando nao validado. Numero de renders maior que a capacidade maxima de 7 ?\n");
			}
		}else if(vetMsg[strlen("clientes")] == '?'){
			printParametroNumerico(vetMsg, configGlobal.config.n_remotos, "Numero renders");
		}
		else{
			sprintf(vetMsg,"? Mensagem nao identificada. Verifique a sintaxe. ?\n");
		}

	}else if(memcmp(vetMsg, "default", strlen("default")) == 0){
		if(vetMsg[strlen("default")] == ':'){
			loadDefault();
			sprintf(vetMsg, "? Carregada a configuracao padrao ?\n");
		}else{
			sprintf(vetMsg,"? Mensagem nao identificada. Verifique a sintaxe. ?\n");
		}

	}else if(memcmp(vetMsg, "reboot", strlen("reboot")) == 0){
		if(vetMsg[strlen("reboot")] == ':'){
			while(1){
				statusBooting();
			}
		}else{
			sprintf(vetMsg,"? Mensagem nao identificada. Verifique a sintaxe. ?\n");
		}
	}else if(memcmp(vetMsg, "inv", strlen("inv")) == 0){
		char * temp = vetMsg;
		if(temp[strlen("inv")] == ':'){
			temp += (strlen("inv") + 1); // danda com a string ate a posicao do parametro

			if(temp[0] == ' '){ // remove o espaco, caso houver
				temp++;
			}
			invEixo(vetMsg, temp[0], "Eixo");
		}else{
			printInvEixos(vetMsg);
		}
	}else if(memcmp(vetMsg, "oe", strlen("oe")) == 0){
		char * temp = vetMsg;
		if(temp[strlen("oe")] == ':'){
			temp += (strlen("oe") + 1);
			if(temp[0] == ' '){
				temp++;
			}
			reordenaEixos(vetMsg, temp);
		}else{
			printOrdemEixos(vetMsg, "");
		}
	}else if(memcmp(vetMsg, "oa", strlen("oa")) == 0){
		char * temp = vetMsg;
		if(temp[strlen("oa")] == ':'){
			temp += (strlen("oa") + 1);
			if(temp[0] == ' '){
				temp++;
			}
			reordenaAngulos(vetMsg, temp);
		}else{
			printOrdemAngulos(vetMsg, "");
		}
	}else if(memcmp(vetMsg, "of", strlen("of")) == 0){

		switch (vetMsg[strlen("of")]) {
			case 'x':
				manipulaOffset(vetMsg,  "Offset Eixo X" , &configGlobal.config.offset_freed.offsetData.x);
			break;
			case 'y':
				manipulaOffset(vetMsg,  "Offset Eixo Y" , &configGlobal.config.offset_freed.offsetData.y);
			break;
			case 'z':
				manipulaOffset(vetMsg,  "Offset Eixo Z" , &configGlobal.config.offset_freed.offsetData.z);
			break;
			case 'p':
				manipulaOffset(vetMsg,  "Offset Eixo PAN" , &configGlobal.config.offset_freed.offsetData.pan);
			break;
			case 't':
				manipulaOffset(vetMsg,  "Offset Eixo TILT" , &configGlobal.config.offset_freed.offsetData.tilt);
			break;
			case 'r':
				manipulaOffset(vetMsg,  "Offset Eixo ROLL" , &configGlobal.config.offset_freed.offsetData.roll);
			break;
			default:
				sprintf(vetMsg,"? Mensagem nao identificada. Verifique a sintaxe. ?\n");
			break;
		}
	}
	else if(memcmp(vetMsg, "?", strlen("?")) == 0){
		switch (vetMsg[1]) {
			case '?':
				printConfig(vetMsg);
				break;
			case '!':
				//memcpy(vetMsg, bufferStatusGeral, strlen(bufferStatusGeral));
				decoderBufferGeral(vetMsg, bufferStatusGeral);
				break;
			case 'D':
			case 'd':
				printDiagnostico(vetMsg);
				break;
			case 'E':
			case 'e':
				printEepromDiag(vetMsg);
				break;
			default:
				printListaCmd(vetMsg);
				break;
		}
	}else if(memcmp(vetMsg, "ver?", strlen("ver?")) == 0){
		sprintf(vetMsg, "? Versao do preset salvo na eeprom: %u ?\n", configGlobal.config.ver);
	}
	else{
		sprintf(vetMsg,"? Mensagem nao identificada. Verifique a sintaxe. ?\n");
	}

	(*sizeOut) = strlen(vetMsg);
}

void ipToArray(char * vetMsg, uint8_t * ret){
	for(uint16_t i = 0; i < 4; i ++){
		ret[i] = atoi(vetMsg);
		vetMsg = strchr(vetMsg, '.') + 1;
	}
}

void alteraIP(char * vetMsg, uint16_t * parametro, char * strMsg){
	uint8_t tempIp[4], validado = 0xff;
	ipToArray((strrchr(vetMsg, ':') + 1), tempIp);

	// Valida todos os parametro antes de salvar na variavel de configuracao
	for(uint8_t i = 0; i < 4; i ++){
		if(tempIp[i] < 0 || tempIp[i] > 255){
			validado = 0;
			break;
		}
	}
	if(validado){
		parametro[0] = tempIp[0];
		parametro[1] = tempIp[1];
		parametro[2] = tempIp[2];
		parametro[3] = tempIp[3];
		sprintf(vetMsg,"? Parametro %s alterado para: %u.%u.%u.%u ?\n",strMsg, parametro[0], parametro[1], parametro[2], parametro[3]);
	}else{
		sprintf(vetMsg,"? Falha na alteracao do parametro %s , verifique o range de valores inseridos ?\n", strMsg);
	}
}

void invEixo(char * vetMsg, char eixo , char * strMsg){
	switch (eixo) {
		case 'x':
			configGlobal.config.invEixos ^= INV_X;
			break;
		case 'y':
			configGlobal.config.invEixos ^= INV_Y;
			break;
		case 'z':
			configGlobal.config.invEixos ^= INV_Z;
			break;
		case 'p':
			configGlobal.config.invEixos ^= INV_PAN;
			break;
		case 't':
			configGlobal.config.invEixos ^= INV_TILT;
			break;
		case 'r':
			configGlobal.config.invEixos ^= INV_ROLL;
			break;
		case 'f':
			configGlobal.config.invEixos ^= INV_FOCO;
			break;
		case 'Z':
			configGlobal.config.invEixos ^= INV_ZOOM;
			break;
		default:
			sprintf(vetMsg, "? Falha na alteracao do parametro, verifique a sintaxe ?\n");
			return;
			break;
	}
	sprintf(vetMsg, "? Alterado o sentido do %s %c ?\n", strMsg, eixo);
}

void reordenaEixos(char * vetMsg, char * cmd){
	if(strcmp(cmd, "xyz") == 0){
		configGlobal.config.ordem_XYZ = XYZ;
		printOrdemEixos(vetMsg, " Ateracao realizada. ");
	}else if(strcmp(cmd, "xzy") == 0){
		configGlobal.config.ordem_XYZ = XZY;
		printOrdemEixos(vetMsg, " Ateracao realizada. ");
	}else if(strcmp(cmd, "yxz") == 0){
		configGlobal.config.ordem_XYZ = YXZ;
		printOrdemEixos(vetMsg, " Ateracao realizada. ");
	}else if(strcmp(cmd, "yzx") == 0){
		configGlobal.config.ordem_XYZ = YZX;
		printOrdemEixos(vetMsg, " Ateracao realizada. ");
	}else if(strcmp(cmd, "zxy") == 0){
		configGlobal.config.ordem_XYZ = ZXY;
		printOrdemEixos(vetMsg, " Ateracao realizada. ");
	}else if(strcmp(cmd, "zyx") == 0){
		configGlobal.config.ordem_XYZ = ZYX;
		printOrdemEixos(vetMsg, " Ateracao realizada. ");
	}else{
		printOrdemEixos(vetMsg, " Falha ao alterar parametro. ");
	}
}

void printOrdemEixos(char * vetMsg, char * strMsg){
	switch (configGlobal.config.ordem_XYZ) {
		case XYZ:
			sprintf(vetMsg, "?%s Ordem de envio dos eixos: XYZ ?\n", strMsg);
			break;
		case XZY:
			sprintf(vetMsg, "?%s Ordem de envio dos eixos: XZY ?\n", strMsg);
			break;
		case YXZ:
			sprintf(vetMsg, "?%s Ordem de envio dos eixos: YXZ ?\n", strMsg);
			break;
		case YZX:
			sprintf(vetMsg, "?%s Ordem de envio dos eixos: YZX ?\n", strMsg);
			break;
		case ZXY:
			sprintf(vetMsg, "?%s Ordem de envio dos eixos: ZXY ? \n", strMsg);
			break;
		case ZYX:
			sprintf(vetMsg, "?%s Ordem de envio dos eixos: ZYX ? \n", strMsg);
			break;
		default:
			break;
	}
}

void reordenaAngulos(char * vetMsg, char * cmd){
	if(strcmp(cmd, "ptr") == 0){
		configGlobal.config.ordem_PTR = PTR;
		printOrdemAngulos(vetMsg, " Ateracao realizada. ");
	}else if(strcmp(cmd, "prt") == 0){
		configGlobal.config.ordem_PTR = PRT;
		printOrdemAngulos(vetMsg, " Ateracao realizada. ");
	}else if(strcmp(cmd, "tpr") == 0){
		configGlobal.config.ordem_PTR = TPR;
		printOrdemAngulos(vetMsg, " Ateracao realizada. ");
	}else if(strcmp(cmd, "trp") == 0){
		configGlobal.config.ordem_PTR = TRP;
		printOrdemAngulos(vetMsg, " Ateracao realizada. ");
	}else if(strcmp(cmd, "rpt") == 0){
		configGlobal.config.ordem_PTR = RPT;
		printOrdemAngulos(vetMsg, " Ateracao realizada. ");
	}else if(strcmp(cmd, "rtp") == 0){
		configGlobal.config.ordem_PTR = RTP;
		printOrdemAngulos(vetMsg, " Ateracao realizada. ");
	}else{
		printOrdemEixos(vetMsg, " Falha ao alterar parametro. ");
	}
}

void printOrdemAngulos(char * vetMsg, char * strMsg){
	switch (configGlobal.config.ordem_PTR) {
		case PTR:
			sprintf(vetMsg, "?%s Ordem de envio dos angulos: PTR ?\n", strMsg);
			break;
		case PRT:
			sprintf(vetMsg, "?%s Ordem de envio dos angulos: PRT ?\n", strMsg);
			break;
		case TPR:
			sprintf(vetMsg, "?%s Ordem de envio dos angulos: TPR ?\n", strMsg);
			break;
		case TRP:
			sprintf(vetMsg, "?%s Ordem de envio dos angulos: TRP ?\n", strMsg);
			break;
		case RPT:
			sprintf(vetMsg, "?%s Ordem de envio dos angulos: RPT ?\n", strMsg);
			break;
		case RTP:
			sprintf(vetMsg, "?%s Ordem de envio dos angulos: RTP ?\n", strMsg);
			break;
		default:
			break;
	}
}

void printInvEixos(char * vetMsg){
	uint16_t temp = configGlobal.config.invEixos;
	sprintf(vetMsg,"? X: %u, Y: %u, Z: %u, PAN: %u, TILT: %u, ROLL: %u, FOCO: %u, ZOOM: %u ?\n", temp & INV_X, (temp & INV_Y) >> 1, (temp & INV_Z) >> 2, (temp & INV_PAN) >> 3, (temp & INV_TILT) >> 4, (temp & INV_ROLL) >> 5, (temp & INV_FOCO) >> 6, (temp & INV_ZOOM) >> 7);
}

void alteraParametroNumerico(char * vetMsg, uint16_t * parametro, char * strMsg){
	(*parametro) = atoi((strrchr(vetMsg, ':') + 1));
	sprintf(vetMsg, "? Parametro %s alterado para o valor: %u ?\n", strMsg, (*parametro));
}

void printIP(char * vetMsg, uint16_t * parametro, char * strMsg){
	sprintf(vetMsg, "? %s : %u.%u.%u.%u ?\n", strMsg, parametro[0], parametro[1], parametro[2], parametro[3]);
}

void printParametroNumerico(char * vetMsg, uint16_t parametro, char * strMsg){
	sprintf(vetMsg, "? %s : %u ?\n", strMsg, parametro);
}

void printStatusDHCP(char * vetMsg, char * msgIni){
	if(configGlobal.config.dhcp_static == 0){
		sprintf(vetMsg, "? %s : ON ?\n", msgIni);
	}else{
		sprintf(vetMsg, "? %s : OFF ?\n", msgIni);
	}
}

void printConfig(char * vetMsg){
	char temp[200];

	sprintf(vetMsg, "? Configuracao geral ?\n");

	printParametroNumerico(temp, configGlobal.config.ver, "Ver");
	strcat(vetMsg,temp);

	printIP(temp, &configGlobal.config.ip1, "IP Server");
	strcat(vetMsg,temp);

	printIP(temp, &configGlobal.config.mask1, "SN");
	strcat(vetMsg,temp);

	printIP(temp, &configGlobal.config.gw1, "GW");
	strcat(vetMsg,temp);

	printIP(temp, &configGlobal.config.dns1, "DNS");
	strcat(vetMsg,temp);

	printStatusDHCP(temp, "Status DHCP");
	strcat(vetMsg,temp);

	printParametroNumerico(temp, configGlobal.config.n_remotos, "Numero renders");
	strcat(vetMsg,temp);

	printIP(temp, &configGlobal.config.ip1_rmt1, "IP R1");
	strcat(vetMsg,temp);

	printParametroNumerico(temp, configGlobal.config.porta_rmt1, "Porta R1");
	strcat(vetMsg,temp);

	printIP(temp, &configGlobal.config.ip1_rmt2, "IP R2");
	strcat(vetMsg,temp);

	printParametroNumerico(temp, configGlobal.config.porta_rmt2, "Porta R2");
	strcat(vetMsg,temp);

	printIP(temp, &configGlobal.config.ip1_rmt3, "IP R3");
	strcat(vetMsg,temp);

	printParametroNumerico(temp, configGlobal.config.porta_rmt3, "Porta R3");
	strcat(vetMsg,temp);

	printIP(temp, &configGlobal.config.ip1_rmt4, "IP R4");
	strcat(vetMsg,temp);

	printParametroNumerico(temp, configGlobal.config.porta_rmt4, "Porta R4");
	strcat(vetMsg,temp);

	printIP(temp, &configGlobal.config.ip1_rmt5, "IP R5");
	strcat(vetMsg,temp);

	printParametroNumerico(temp, configGlobal.config.porta_rmt5, "Porta R5");
	strcat(vetMsg,temp);

	printIP(temp, &configGlobal.config.ip1_rmt6, "IP R6");
	strcat(vetMsg,temp);

	printParametroNumerico(temp, configGlobal.config.porta_rmt6, "Porta R6");
	strcat(vetMsg,temp);

	printIP(temp, &configGlobal.config.ip1_rmt7, "IP R7");
	strcat(vetMsg,temp);

	printParametroNumerico(temp, configGlobal.config.porta_rmt7, "Porta R7");
	strcat(vetMsg,temp);

	printOrdemEixos(temp, "");
	strcat(vetMsg,temp);

	printOrdemAngulos(temp, "");
	strcat(vetMsg,temp);

	printInvEixos(temp);
	strcat(vetMsg,temp);

	printOffsets(temp);
	strcat(vetMsg,temp);
}

void decoderBufferGeral(char * vetMsg, uint8_t * bufferGeral){
	char temp[100];

	sprintf(vetMsg, "? ");

	if(bufferGeral[0] & (1 << 0)){
		sprintf(temp, "RX:NOVO, ");
	}else{
		sprintf(temp, "RX:VELHO, ");
	}
	strcat(vetMsg,temp);

	if(bufferGeral[0] & (1 << 1)){
		sprintf(temp, "RX:OK, ");
	}else{
		sprintf(temp, "RX:N_OK, ");
	}
	strcat(vetMsg,temp);

	if(bufferGeral[0] & (1 << 2)){
		sprintf(temp, "SYNC:ON, ");
	}else{
		sprintf(temp, "SYNC:OFF, ");
	}
	strcat(vetMsg,temp);

	sprintf(temp, "ERROR UART: %u ", bufferGeral[1]);
	strcat(vetMsg,temp);

	sprintf(temp, "ERROR SPI: %u ?\n", bufferGeral[2]);

	strcat(vetMsg,temp);
}

void printDiagnostico(char *vetMsg)
{
    uint32_t agora = HAL_GetTick();

    uint32_t totalSegundos = agora / 1000U;

    uint32_t dias = totalSegundos / 86400U;
    uint32_t horas = (totalSegundos % 86400U) / 3600U;
    uint32_t minutos = (totalSegundos % 3600U) / 60U;
    uint32_t segundos = totalSegundos % 60U;

    snprintf(vetMsg, 2048,
        "?D UART_ERR:%lu LAST:0x%08lX ORE:%lu FE:%lu NE:%lu PE:%lu "
        "FRAME_BAD:%lu RX_TOTAL:%lu RX_OK:%lu RX_BAD:%lu "
        "LAST_RX_EVENT_MS:%lu LAST_RX_OK_MS:%lu "
        "SYNC_TOTAL:%lu SYNC_LOST:%lu LAST_SYNC_MS:%lu "
        "FREED_TOTAL:%lu FREED_OK:%lu FREED_ERR:%lu FREED_DROP:%lu LAST_FREED_MS:%lu "
        "FREED_LAST_RET:%ld FREED_LAST_SOCKET:%lu "
        "ETH_LINK:%lu ETH_DOWN:%lu ETH_UP:%lu "
        "UPTIME:%lud %02lu:%02lu:%02lu "
        "TCP_ACCEPT:%lu TCP_RX_OK:%lu TCP_RX_ERR:%lu "
        "TCP_SEND_CALL:%lu TCP_SEND_RET_OK:%lu TCP_SEND_RET_ERR:%lu "
        "TCP_SENDOK:%lu TCP_TIMEOUT:%lu TCP_CLOSE_WAIT:%lu "
        "TCP_DISCON:%lu TCP_REOPEN:%lu "
        "TCP_LAST_SR:0x%02lX TCP_LAST_IR:0x%02lX "
        "TCP_LAST_RECV:%ld TCP_LAST_SEND:%ld TCP_LAST_TX_FSR:%lu "
        "TCP_LISTEN:%lu TCP_CLOSED_LISTEN_MS:%lu TCP_MAX_CLOSED_LISTEN_MS:%lu ?\n",

        (unsigned long)contadorErroUART,
        (unsigned long)ultimoErroUART,
        (unsigned long)contadorErroORE,
        (unsigned long)contadorErroFE,
        (unsigned long)contadorErroNE,
        (unsigned long)contadorErroPE,
        (unsigned long)contadorFalhaEnquadramentoUART,
        (unsigned long)contadorRXTotal,
        (unsigned long)contadorRXValido,
        (unsigned long)contadorRXInvalido,
        (unsigned long)(agora - ultimoRXTick),
        (unsigned long)(agora - ultimoRXOkTick),
        (unsigned long)contadorSyncTotal,
        (unsigned long)contadorSyncLost,
        (unsigned long)(agora - ultimoSyncTick),
        (unsigned long)contadorFreeDTotal,
        (unsigned long)contadorFreeDOk,
        (unsigned long)contadorFreeDErro,
        (unsigned long)contadorFreeDDropped,
        (unsigned long)(agora - ultimoFreeDTick),
        (long)ultimoFreeDRetorno,
        (unsigned long)ultimoFreeDSocket,
        (unsigned long)estadoLinkEthernet,
        (unsigned long)contadorEthDown,
        (unsigned long)contadorEthUp,

        (unsigned long)dias,
        (unsigned long)horas,
        (unsigned long)minutos,
        (unsigned long)segundos,
        (unsigned long)tcpAcceptCount,
        (unsigned long)tcpRxOkCount,
        (unsigned long)tcpRxErrCount,
        (unsigned long)tcpSendCallCount,
        (unsigned long)tcpSendRetOkCount,
        (unsigned long)tcpSendRetErrCount,
        (unsigned long)tcpSendOkCount,
        (unsigned long)tcpTimeoutCount,
        (unsigned long)tcpCloseWaitCount,
        (unsigned long)tcpDisconCount,
        (unsigned long)tcpReopenCount,
        (unsigned long)tcpLastSr,
        (unsigned long)tcpLastIr,
        (long)tcpLastRecv,
        (long)tcpLastSend,
        (unsigned long)tcpLastTxFsr,
        (unsigned long)tcpListenCount,
        (unsigned long)tcpClosedToListenMs,
        (unsigned long)tcpMaxClosedToListenMs
    );
}

void manipulaOffset(char * vetMsg,  char *msgPrint, float * data){

	if(vetMsg[strlen("of")+1] == ':'){
		(*data) = (float)atof((strrchr(vetMsg, ':') + 1));
		sprintf(vetMsg, "? Alterado %s para : %f ?\n", msgPrint, (*data));
	}else{
		sprintf(vetMsg, "? %s : %f ?\n", msgPrint, (*data));
	}
}

void printOffsets(char * vetMsg){
	sprintf(vetMsg,"? OffsetX : %f, OffsetY : %f, OffsetZ : %f, OffsetPan : %f, OffsetTilt : %f, OffsetRoll : %f ?\n",
																		configGlobal.config.offset_freed.offsetData.x,
																		configGlobal.config.offset_freed.offsetData.y,
																		configGlobal.config.offset_freed.offsetData.z,
																		configGlobal.config.offset_freed.offsetData.pan,
																		configGlobal.config.offset_freed.offsetData.tilt,
																		configGlobal.config.offset_freed.offsetData.roll);
}

void printListaCmd(char * vetMsg){

	sprintf(vetMsg, "? Lista de comandos para alteracao e verificacao de parametros ?\n"
					"? Argumento ':' indica alteracao e argumento '?' indica consulta ?\n"
					"? 'ip:xx.xx.xx.xx' altera o ip da caixa de track ?\n"
					"? 'ip?' consulta o ip da caixa de track ?\n"
					"? 'sn' altera(:xx.xx.xx.xx) ou consulta(?) a subnet da caixa de track ?\n"
					"? 'gw' altera(:xx.xx.xx.xx) ou consulta(?) o gateway da caixa de track ?\n"
					"? 'dns' altera(:xx.xx.xx.xx) ou consulta(?) o DNS da caixa de track ?\n"
					"? 'dhcp' altera(:on ou :off) ou consulta(?) o DHCP da caixa de track ?\n"
					"? 'clientes' altera(: xx) ou consulta(?) o numero (xx) de conexoes possiveis de clientes ?\n"
					"? 'ipX' altera(:xx.xx.xx.xx) ou consulta(?) o ip do render X da caixa de track ?\n"
					"? 'portaX' altera(: xx) ou consulta(?) a porta de envio para o render X da caixa de track ?\n"

					"? 'inv' altera (:x ou :y ou :z ou :p ou :t ou :r ou :f ou :Z) o sentido dos eixos X,Y,Z,PAN, TILT, ROLL, FOCO OU ZOOM, ou (?) consulta a configuracao atual ?\n"
					"? 'oe' altera (:xyz ou :yxz) a ordem de envio dos dados do freed em relacao aos eixos da camera, ou exibe (?) a ordem atual ?\n"
					"? 'oa' altera (:ptr ou :tpr) a ordem de envio dos dados do freed em relacao aos angulos da camera, ou exibe (?) a ordem atual ?\n"

					"? 'ofx: xx.xx' altera o offset do freed do eixo X. Demais eixos (y,z, pan, tilt, roll) : 'ofy:xx.xx', 'ofz:xx.xx', 'ofp:xx.xx', 'oft:xx.xx' ou 'ofr:xx.xx' ?\n"
					"? 'ofx?' imprime o valor de offset do eixo x. Idem para demais eixos, alterando apenas a letra do eixo, 'y', 'z', 'p', 't' ou 'r' ?\n"

					"? 'salva:' salvas as configuracoes atuais na memoria permanente da caixa de track ?\n"
					"? 'default:' carrega na memória de execucao a configuracao default da caixa de track ?\n"
					"? 'reboot:' reinicia a caixa de track ?\n"

					"? ver? Exibe o numero da versao salva no preset na eeprom ?\n"
					"? '?!' Exibe o status geral do ssitema ?\n"
					"? '??' Exibe a configuracao atalmente carregada ?\n"
					"? '?' Exibe esse menu ?\n"
			);
}
