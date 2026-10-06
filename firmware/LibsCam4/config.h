/*
 * config.h
 *
 *  Created on: 15 de jul de 2022
 *      Author: danie
 */

#ifndef CONFIG_H_
#define CONFIG_H_

#ifdef __cplusplus
extern "C"
{
#endif /* __cplusplus */

#include <string.h>
#include <stdio.h>
#include <stdlib.h>
#include "eeprom.h"
#include "w5500_spi.h"
#include "wizchip_conf.h"

#define VER_DEFAULT 0xffff		// Versao padrao do preset global, ao iniciar com tudo zerado na eeprom, essa é a versao carregada.

typedef enum{					// condigo para inversao dos eixos de posicao
  XYZ = 0,
  XZY,
  YXZ,
  YZX,
  ZXY,
  ZYX
}ordemXYZ;

typedef enum{					// codigo para inversao dos eixos de rotacao
	PTR = 0,
	PRT,
	RPT,
	RTP,
	TPR,
	TRP
}ordemPTR;

typedef enum{					// posicao dos bits da configuracao ativa na palabra de configuracao
	COMP_XY 	   		= (1 << 0),
	COMP_Z 		  	    = (1 << 1),
	COMP_ROT 	   		= (1 << 2),
	COMP_ROL 	   		= (1 << 3),
	COMP_MODO_ROL_DINAM = (1 << 4)
};

typedef enum{					// Posicao da mascara para inversao do bit
	INV_X	 = (1 << 0),
	INV_Y	 = (1 << 1),
	INV_Z 	 = (1 << 2),
	INV_PAN  = (1 << 3),
	INV_TILT = (1 << 4),
	INV_ROLL = (1 << 5),
	INV_FOCO = (1 << 6),
	INV_ZOOM = (1 << 7)
};

typedef struct {
	float x, y, z, pan, tilt, roll;
} offsetEixos_ST;


typedef union {
	offsetEixos_ST offsetData;
	uint16_t vetOffset[6*4];
} offset_ST_UNION;

typedef struct __attribute__((__packed__)) {		// estrutura da configuracao da aplicacao

	uint16_t ver;
	uint16_t mac1, mac2, mac3, mac4, mac5, mac6;
	uint16_t ip1, ip2, ip3, ip4;
	uint16_t mask1, mask2, mask3, mask4;
	uint16_t gw1, gw2, gw3, gw4;
	uint16_t dns1, dns2, dns3, dns4;
	uint16_t dhcp_static;

	uint16_t n_remotos;
	uint16_t ip1_rmt1, ip2_rmt1, ip3_rmt1, ip4_rmt1, porta_rmt1;
	uint16_t ip1_rmt2, ip2_rmt2, ip3_rmt2, ip4_rmt2, porta_rmt2;
	uint16_t ip1_rmt3, ip2_rmt3, ip3_rmt3, ip4_rmt3, porta_rmt3;
	uint16_t ip1_rmt4, ip2_rmt4, ip3_rmt4, ip4_rmt4, porta_rmt4;
	uint16_t ip1_rmt5, ip2_rmt5, ip3_rmt5, ip4_rmt5, porta_rmt5;
	uint16_t ip1_rmt6, ip2_rmt6, ip3_rmt6, ip4_rmt6, porta_rmt6;
	uint16_t ip1_rmt7, ip2_rmt7, ip3_rmt7, ip4_rmt7, porta_rmt7;

	uint16_t invEixos; 					    // X, Y, Z, P, T , R, FOCO, ZOOM (BIT0-X)
	uint16_t ordem_XYZ, ordem_PTR;          // Indica a ordem que os eixos seram enviados pelo freeD
	uint16_t configComp;					// Bits indicam qual compesacao esta habilitada - bit0 xy, bit1 z, bit2 rol, bit3 rotacao, bit 4 pan_tilt_dinamico.

	offset_ST_UNION offset_freed;
} config_ST;

typedef union {
	config_ST config;
	uint16_t vetConfig[sizeof(config_ST) / 2];
	uint8_t vetByte[sizeof(config_ST)];
}configTrack;

uint8_t configInit(uint16_t * virtualAddr, uint16_t n_addrs);


void loadDefault(void);
uint8_t loadConfig(void);
uint8_t gravaConfig(void);
void SERVER_MENU(uint8_t * buffetStatusGeral, uint16_t sizeBufferStatusGeral, uint8_t * bufferMgsOut, uint16_t sizeBufferMsgOut, uint8_t socketNumber, uint16_t PORTA, uint16_t flag);
void check_menu(char * bufferStatusGeral, char * vetMsg, uint16_t * sizeOut);
void ipToArray(char * string, uint8_t * ret);
void alteraIP(char * vetMsg, uint16_t * parametro, char * strMsg);
void printParametroNumerico(char * vetMsg, uint16_t parametro, char * strMsg);
void alteraParametroNumerico(char * vetMsg, uint16_t * parametro, char * strMsg);
void printIP(char * vetMsg, uint16_t * parametro, char * strMsg);
void invEixo(char * vetMsg, char eixo , char * strMsg);
void printInvEixos(char * vetMsg);
void reordenaEixos(char * vetMsg, char * cmd);
void printOrdemEixos(char * vetMsg, char * strMsg);
void reordenaAngulos(char * vetMsg, char * cmd);
void printOrdemAngulos(char * vetMsg, char * strMsg);
void decoderBufferGeral(char * vetMsg, uint8_t * bufferGeral);
void printListaCmd(char * vetMsg);
void printStatusDHCP(char * vetMsg, char * msgIni);
void printOffsets(char * vetMsg);
void manipulaOffset(char * vetMsg,  char *msgPrint, float * data);

#ifdef __cplusplus
};
#endif /* __cplusplus */


#endif /* CONFIG_H_ */
