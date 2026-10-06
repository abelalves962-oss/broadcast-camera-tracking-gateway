#ifndef DECODERTRACK_H_
#define DECODERTRACK_H_


#ifdef __cplusplus
extern "C"
{
#endif /* __cplusplus */

/*
  EIXOS CAMERA (VISTA SUPERIOR)        |  EIXO GRAFISMO (VISTA SUPERIRO Z SAINDO DO PLANO)
    EIXO Y                             |  		 EIXO x
    ^                                  |              ^
    +                                  |              +
    +                                  |              +
    + + + > Eixo X                     |  EIXO Y <+ + +
                                       |
                                       |
////////////////////////////////////////////////////////////////////////////////////////////

  EIXO GRAFISMO

*/
#include <math.h>
#include "FreeD.h"
#include <stdint.h>
#include "config.h"

/*
  bytes 0 - 5 -> marcador ??
  bytes 06-09 --> PAN (Float - Little Endian (DCBA)) - VATOR DE CONVERSAO: DIVIDIR POR 5028 PARA LER O VALOR EM GRAUS OU MULTIPLICAR GRAUS  * 5028 PARA OBTER O VALOR NO FORMADO DOS DADOS
  bytes 10-13 --> TILT (Float - Little Endian (DCBA)) - VATOR DE CONVERSAO: DIVIDIR POR 5028 PARA LER O VALOR EM GRAUS OU MULTIPLICAR GRAUS  * 5028 PARA OBTER O VALOR NO FORMADO DOS DADOS
  bytes 14-17 --> TRACK (Float - Little Endian (DCBA)) - VATOR DE CONVERSAO: DIVIDIR POR 320 PARA LER O VALOR EM GRAUS OU MULTIPLICAR GRAUS  * 320 PARA OBTER O VALOR NO FORMADO DOS DADOS
  bytes 18-21 --> ZOOM (Float - Little Endian (DCBA)) - VALOR BRUTO 0 - 65354
  bytes 22-25 --> FOCO (Float - Little Endian (DCBA)) - VALOR BRUTO 0 - 65354
  byte 26 ->> DESCONHECIDO - OBSERVADO VALOR VARIANDO DE 0X12 PARA 0X02
*/

// TODOS AS MEDIDAS DE TAMANHO ESTAO EM MM e ANGULOS EM RADIANOS


///*****************  DEFINES PARA DEBUG  *********************************************************/

//#define DEBUG_PRINT
//#define DEBUG_DEBUG_PRINT_ZOOM

///****************** DEFINES APLICACAO MAPAS COMP **********************************************/

#define APLICA_MAPA_ROT_PAN
#define APLICA_MAPA_XY
#define APLICA_MAPA_Z
#define APLICA_MAPA_TORCAO_ROLL_TRILHO
#define APLICA_ROLL_TILT_DINAMICO_PAN

#define COMP_BELZIER
#define EXP_BELZIER_N 3		// Expodente da curva belzier configurável, para a compensacao


///***********************  DEFINES FOLGA ZOOM ****************************************************/
#define ANTI_BACKSLASH_LENTE
#define TAMANHO_GAP_ZOOM	400				   // Tamanhod a folga do zoom ao mudar a direcao de atuacao
#define REESCALA_ZOOM		(float)	(65535.0 / (65535.0 - TAMANHO_GAP_ZOOM))

//////********************************************************************************************/

#define	SIZEPACKET			27				   // Tamanho do pacote enviodo pelo remote de opercao
#define MarcadorInicioPac	0x262A212F         // Marcador de 4 bytes do inicio do pacote
#define DELIMITADOR2 		0x0103			   // Marcador dos bytes 5 e 6 do pacote

#define ANGULO_TRILHO_VIGA	(float) 0.0 * (M_PI / 180.0) // Synthetic public example; calibrate for the target installation.

#define ANG_TILT_TRILHO		(float)  0 //-0.06 //-0.06

#define DELTA_X_PAN_NODAL	(float) (0.0) // Synthetic public example.
#define DELTA_Y_PAN_NODAL	(float) (0.0) // Synthetic public example.
#define DELTA_Y_TILT_NODAL	(float) (0.0) // Synthetic public example.
#define DELTA_Z_TILT_NODAL	(float) (0.0) // Synthetic public example.

#define HIPO_DESL_TILT		(float) sqrt(DELTA_X_TILT_NODAL*DELTA_X_TILT_NODAL + DELTA_Y_TILT_NODAL*DELTA_Y_TILT_NODAL)

//#define LARGURA_BATENTE		(float) 260.0

#define X0                  		(float) 0.0
#define Y0                  		(float) 0.0
#define Z0                  		(float) 1000.0 // Synthetic public example.
#define TAMANHO_TRECHO_RETO 		(float) 1000.0 // Synthetic public example.
#define RAIO_CORREIA				(float) 2000.0 // Synthetic public example.
#define RAIO_CAMERA         		(float) 2000.0 // Synthetic public example.
#define X0C1                		(float) (X0 - RAIO_CAMERA)             		// PONTO X DO CENTRO DO RAIO DA CURVA 1
#define Y0C1                		(float) (Y0 - TAMANHO_TRECHO_RETO)     		// PONTO Y DO CENTRO DO RAIO DA CURVA 1
#define Z0C1                		(float) Z0
#define TETHA0_R1           		(float) (-asin(((Y0 -TAMANHO_TRECHO_RETO) - Y0C1)/RAIO_CAMERA))      // ANGULO INICIAL DO RAIO 1, EM RELAÇÃO AO PONTO DE JUNÇÃO COM O TRECHO RETO
#define OFFSET_PAN_TRANSFORM_DISP	(float) 0.0
#define OFFSET_PAN_TRANSFORM		(float) OFFSET_PAN_TRANSFORM_DISP
#define OFFSET_TILT_TRANSFORM		(float) 0.0
#define OFFSET_ROLL_TRANSFORM   	(float) 0.0									// OFFSET ATUA NOS CALCULOS
#define OFFSET_PAN_FREED    		(float) 0.0 								// OFFSET APENAS NA SAIDA DO FREED
#define OFFSET_TILT_FREED			(float) 0.0									// OFFSET APENAS NA SAIDA DO FREED
#define OFFSET_ROLL_FREED			(float) 0.0									// OFFSET APENAS NA SAIDA DO FREED
#define OFFSET_X_FREED        		(float) 0.0
#define OFFSET_Y_FREED         		(float) 0.0
#define OFFSET_Z_FREED	    		(float) - DELTA_Z_TILT_NODAL

#define NORMALIZA_ID_BUF_UP(ID, SZ) ((ID >= SZ) ? (0) : (ID))
#define NORMALIZA_ID_BUF_DW(ID, SZ) ((ID < 0) ? (SZ-1) : (ID))

typedef struct
{
    float x,y,z,theata;
}coordenadasYX;

 typedef struct __attribute__((__packed__)) {
	  uint32_t marcador1;
	  uint16_t marcador2;
	  float pan;
	  float tilt;
	  float track;
	  float zoom;
	  float foco;
	  uint8_t byteFinal;
  } dadoSeparado_ST;

typedef struct {
	float lastRawValue, backSlashAtual, zoomCorrigido;
} gap_zoom;

typedef struct{
	float distComp;    		// varicacao do valor do eixo da absicao, variando de 0 a 1 em relacao a distancia para a compensacao entre dois ponto
	float lastCompOffset; 	// ultimo valor obtido da compensacao, para o caso do ponto atual ser igual ao anterir, na tabela, para que a compensacao seja mantida.
	int16_t lastPoseVet, proxPosVet; // inicilizar em -1, ao criar a variavel
	uint16_t vetSize;
} dadosMapaTrack;

typedef union {
  uint8_t bufferRaw[27]; // BUFFER COM O DOBRO DO TAMANHO DO PACOTE, SÓ PARA EVITAR LEIUTRA EM REGIAO QUE NAO EXISTE
  dadoSeparado_ST dadosSeprados;
  
} bufferData;

coordenadasYX trackParaXY(float track, float panRaw, float tiltRaw, float rollRaw, float * comp);
float range180(float pan);
void CamDataToEixos(FreeD_D1_t * pacoteFreed, bufferData * bufferIN, uint32_t CAM_ID, gap_zoom * dadosZoom,
					dadosMapaTrack * mapaXY, dadosMapaTrack * mapaZ, dadosMapaTrack * mapaPAN, dadosMapaTrack * mapaROLL,
					float const vetdataXY[][3], float const vetdataZ[][2], float const vetdataPAN[][2], float const vetdataROLL[][2]);

void CamDataToFreeD(uint8_t * bufferOut, bufferData * bufferCAM4, uint32_t CAM_ID, gap_zoom * dadosZoom,
				    dadosMapaTrack * mapaXY, dadosMapaTrack * mapaZ, dadosMapaTrack * mapaPAN, dadosMapaTrack * mapaROLL,
					float const vetdataXY[][3], float const vetdataZ[][2], float const vetdataPAN[][2], float const vetdataROLL[][2]);

void exec_mapa_1D(dadosMapaTrack * mapa, float trackData, float * vetComp, float const vetdata[][2]);
void exec_mapa_2D(dadosMapaTrack * mapa, float trackData, float * vetComp, float const vetdata[][3]);

void printSetup(void);

float EaseBelzier(float val);
float EaseBelzierN(float val, float alpha);
float EaseInOutQuad(float val);
float EaseInOutCubic(float val);


#endif //DECODERTRACK_H_
