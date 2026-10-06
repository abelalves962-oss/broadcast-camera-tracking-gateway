#include <decoderTrack.h>

extern configTrack configGlobal; 			// Variavel que carrega as configuracoes em uso. Deve ser declarada na main

coordenadasYX trackParaXY(float track, float panRaw, float tiltRaw, float rollRaw, float * compXYZ){
    coordenadasYX temp1, temp2;
    if (track <= TAMANHO_TRECHO_RETO) { // Trecho reto do percurso
        temp1.theata = 0.0;
        temp1.x = X0;
        temp1.y = Y0 - track;
        temp1.z = 0; // Preenchido na compensacao
    }
    else{
        temp1.theata = (track - TAMANHO_TRECHO_RETO)/(RAIO_CORREIA);
        temp1.x = X0C1 + RAIO_CAMERA*cos(TETHA0_R1 + temp1.theata); // + (DELTA_X_PAN_NODAL*cos(panRaw) - DELTA_Y_PAN_NODAL*sin(panRaw));
        temp1.y = Y0C1 - RAIO_CAMERA*sin(TETHA0_R1 + temp1.theata); // + (DELTA_X_PAN_NODAL*sin(panRaw) + DELTA_Y_PAN_NODAL*cos(panRaw));
        temp1.z = 0; // Preenchido na compensacao												// + (DELTA_Y_TILT_NODAL*sin(tiltRaw) + DELTA_Z_TILT_NODAL*cos(tiltRaw));
    }

    panRaw += temp1.theata;        // Aplica rotacao do trilho para incluir esse valor no calculo do nodal

    ///*********** REALIZA A COMPENSACAO DOS NODAIS, DEVIDO AS ROTACOES E PAN, TILT E ROL *///////////////////////////////

    float nodalZ = (DELTA_Z_TILT_NODAL*cos(-rollRaw) - DELTA_X_PAN_NODAL*sin(-rollRaw));  // rotacao em x devido a pan
   	float nodalX = (DELTA_Z_TILT_NODAL*sin(-rollRaw) + DELTA_X_PAN_NODAL*cos(-rollRaw));  // rotacao em y devido a pan

   	float nodalY = (DELTA_Y_TILT_NODAL*cos(tiltRaw) - nodalZ*sin(tiltRaw));
   	nodalZ		 = (DELTA_Y_TILT_NODAL*sin(tiltRaw) + nodalZ*cos(tiltRaw));

   	float tempNodalX = nodalX;
   	float tempNodalY = nodalY;

   	nodalX		 = tempNodalX*cos(-panRaw) - tempNodalY*sin(-panRaw);
   	nodalY		 = tempNodalX*sin(-panRaw) + tempNodalY*cos(-panRaw);
    ///////////////////**********************************************************************/////////////////////////////

#ifdef DEBUG_PRINT
   	printf("NodalX : %f, Nodal Y: %f, NodalZ: %f, CompX: %f, CompY: %f, CompZ: %f , RotaTrilho: %f ", nodalX, nodalY, nodalZ, compXYZ[0], compXYZ[1], compXYZ[2], temp1.theata*(180.0 / M_PI));
#endif

    temp2.x = compXYZ[0] * (temp1.x*cos(ANGULO_TRILHO_VIGA) - temp1.y*sin(ANGULO_TRILHO_VIGA)) + nodalX;
	temp2.y = compXYZ[1] * (temp1.x*sin(ANGULO_TRILHO_VIGA) + temp1.y*cos(ANGULO_TRILHO_VIGA)) + nodalY;
	temp2.z = compXYZ[2] + (temp1.z)  														   + nodalZ;  // Em Z a compensacao e por offset

	temp2.theata = temp1.theata;
    return temp2;
}

void CamDataToEixos(FreeD_D1_t * pacoteFreed, bufferData * bufferIN, uint32_t CAM_ID, gap_zoom * dadosZoom,
					dadosMapaTrack * mapaXY, dadosMapaTrack * mapaZ, dadosMapaTrack * mapaPAN, dadosMapaTrack * mapaROLL,
					float const vetdataXY[][3], float const vetdataZ[][2], float const vetdataPAN[][2], float const vetdataROLL[][2]){

	coordenadasYX retTrackData;

	float z_reff = 0, roll_ref = 0, panRaw, panRad, tiltRaw, compXYZ[] = {1,1,0}, offsetCompPAN = 0, offsetCompROLL = 0, trilhoData = bufferIN->dadosSeprados.track/320.0;

#ifdef APLICA_MAPA_ROT_PAN
    exec_mapa_1D(mapaPAN, trilhoData, &offsetCompPAN, vetdataPAN);
#endif

    // Valor de pan usado para os calculos de transformacao e posicao final, antes do offset do freed
    panRaw  = (-bufferIN->dadosSeprados.pan / 5028.0) + OFFSET_PAN_TRANSFORM + offsetCompPAN;

#ifdef APLICA_MAPA_TORCAO_ROLL_TRILHO

    exec_mapa_1D(mapaROLL, trilhoData, &offsetCompROLL, vetdataROLL);
    panRad = ((-bufferIN->dadosSeprados.pan / 5028.0) + OFFSET_PAN_TRANSFORM_DISP) * (M_PI / 180.0);

  #ifdef APLICA_ROLL_TILT_DINAMICO_PAN
    roll_ref = offsetCompROLL*cosf(panRad) + OFFSET_ROLL_TRANSFORM;
	tiltRaw = (-range180(bufferIN->dadosSeprados.tilt / 5028.0)) + OFFSET_TILT_TRANSFORM + offsetCompROLL*sinf(panRad);

  #else
	roll_ref = 0 + OFFSET_ROLL_TRANSFORM + offsetCompROLL;
	tiltRaw = (-range180(bufferIN->dadosSeprados.tilt / 5028.0)) + OFFSET_TILT_TRANSFORM;
  #endif

#else
    tiltRaw = (-range180(bufferIN->dadosSeprados.tilt / 5028.0)) + OFFSET_TILT_TRANSFORM;
    roll_ref = OFFSET_ROLL_TRANSFORM;
#endif

#ifdef APLICA_MAPA_XY
    exec_mapa_2D(mapaXY, trilhoData, compXYZ, vetdataXY);
#endif

#ifdef APLICA_MAPA_Z
	exec_mapa_1D(mapaZ, trilhoData, &compXYZ[2], vetdataZ);
	z_reff = 0;
#else
	z_reff = Z0;
#endif

	retTrackData       = trackParaXY(bufferIN->dadosSeprados.track/320.0, panRaw*(M_PI/180.0),  tiltRaw*(M_PI/180.0), roll_ref*(M_PI/180.0), compXYZ);

#ifdef DEBUG_PRINT
	printf("CompPAN: %f , compROLL: %f , compZ: %f \n", offsetCompPAN, offsetCompROLL, compXYZ[2]);
#endif

	pacoteFreed->ID    = CAM_ID;

	switch (configGlobal.config.ordem_XYZ) {
		case XYZ:
			pacoteFreed->X     = retTrackData.x + OFFSET_X_FREED;
			pacoteFreed->Y     = retTrackData.y + OFFSET_Y_FREED;
			pacoteFreed->Z     = z_reff + retTrackData.z + OFFSET_Z_FREED;
			break;
		case XZY:
			pacoteFreed->X     = retTrackData.x + OFFSET_X_FREED;
			pacoteFreed->Y     = z_reff + retTrackData.z + OFFSET_Z_FREED;
			pacoteFreed->Z     = retTrackData.y + OFFSET_Y_FREED;
			break;
		case YXZ:
			pacoteFreed->X     = retTrackData.y + OFFSET_Y_FREED;
			pacoteFreed->Y     = retTrackData.x + OFFSET_X_FREED;
			pacoteFreed->Z     = z_reff + retTrackData.z + OFFSET_Z_FREED;
			break;
		case YZX:
			pacoteFreed->X     = retTrackData.y + OFFSET_Y_FREED;
			pacoteFreed->Y     = z_reff +  retTrackData.z + OFFSET_Z_FREED;
			pacoteFreed->Z     = retTrackData.x + OFFSET_X_FREED;
			break;
		case ZXY:
			pacoteFreed->X     = z_reff + retTrackData.z + OFFSET_Z_FREED;
			pacoteFreed->Y     = retTrackData.x + OFFSET_X_FREED;
			pacoteFreed->Z     = retTrackData.y + OFFSET_Y_FREED;
			break;
		case ZYX:
			pacoteFreed->X     = z_reff + retTrackData.z + OFFSET_Z_FREED;
			pacoteFreed->Y     = retTrackData.y + OFFSET_Y_FREED;
			pacoteFreed->Z     = retTrackData.x + OFFSET_X_FREED;
			break;
		default:
			pacoteFreed->X     = retTrackData.x + OFFSET_X_FREED;
			pacoteFreed->Y     = retTrackData.y + OFFSET_Y_FREED;
			pacoteFreed->Z     = z_reff + retTrackData.z + OFFSET_Z_FREED;
			break;
	}

	switch (configGlobal.config.ordem_PTR) {
		case PTR:
			pacoteFreed->Pan   = range180(panRaw + retTrackData.theata * (180.0 / M_PI) + OFFSET_PAN_FREED);
			pacoteFreed->Tilt  = tiltRaw + OFFSET_TILT_FREED;
			pacoteFreed->Roll  = roll_ref + OFFSET_ROLL_FREED;
			break;
		case PRT:
			pacoteFreed->Pan   = range180(panRaw + retTrackData.theata * (180.0 / M_PI) + OFFSET_PAN_FREED);
			pacoteFreed->Tilt  = roll_ref + OFFSET_ROLL_FREED;
			pacoteFreed->Roll  = tiltRaw + OFFSET_TILT_FREED;
			break;
		case TPR:
			pacoteFreed->Pan   = tiltRaw + OFFSET_TILT_FREED;
			pacoteFreed->Tilt  = range180(panRaw + retTrackData.theata * (180.0 / M_PI) + OFFSET_PAN_FREED);
			pacoteFreed->Roll  = roll_ref + OFFSET_ROLL_FREED;
			break;
		case TRP:
			pacoteFreed->Pan   = tiltRaw + OFFSET_TILT_FREED;
			pacoteFreed->Tilt  = roll_ref + OFFSET_ROLL_FREED;
			pacoteFreed->Roll  = range180(panRaw + retTrackData.theata * (180.0 / M_PI) + OFFSET_PAN_FREED);
			break;
		case RTP:
			pacoteFreed->Pan   = roll_ref + OFFSET_ROLL_FREED;
			pacoteFreed->Tilt  = tiltRaw + OFFSET_TILT_FREED;
			pacoteFreed->Roll  = range180(panRaw + retTrackData.theata * (180.0 / M_PI) + OFFSET_PAN_FREED);
			break;
		case RPT:
			pacoteFreed->Pan   = roll_ref + OFFSET_ROLL_FREED;
			pacoteFreed->Tilt  = range180(panRaw + retTrackData.theata * (180.0 / M_PI) + OFFSET_PAN_FREED);
			pacoteFreed->Roll  = tiltRaw + OFFSET_TILT_FREED;
			break;
		default:
			pacoteFreed->Pan   = range180(panRaw + retTrackData.theata * (180.0 / M_PI) + OFFSET_PAN_FREED);
			pacoteFreed->Tilt  = tiltRaw + OFFSET_TILT_FREED;
			pacoteFreed->Roll  = roll_ref + OFFSET_ROLL_FREED;
			break;
	}

	pacoteFreed->X += configGlobal.config.offset_freed.offsetData.x;
	pacoteFreed->Y += configGlobal.config.offset_freed.offsetData.y;
	pacoteFreed->Z += configGlobal.config.offset_freed.offsetData.z;

	pacoteFreed->Pan += configGlobal.config.offset_freed.offsetData.pan;
	pacoteFreed->Tilt += configGlobal.config.offset_freed.offsetData.tilt;
	pacoteFreed->Roll += configGlobal.config.offset_freed.offsetData.roll;

	pacoteFreed->Pan = range180(pacoteFreed->Pan);
	pacoteFreed->Tilt = range180(pacoteFreed->Tilt);
	pacoteFreed->Roll = range180(pacoteFreed->Roll);

	pacoteFreed->Focus = bufferIN->dadosSeprados.foco;
	pacoteFreed->Zoom  = bufferIN->dadosSeprados.zoom;

#ifdef ANTI_BACKSLASH_LENTE

	if(bufferIN->dadosSeprados.zoom > dadosZoom->lastRawValue){
		dadosZoom->backSlashAtual += (bufferIN->dadosSeprados.zoom - dadosZoom->lastRawValue);
		if(dadosZoom->backSlashAtual > TAMANHO_GAP_ZOOM){
			dadosZoom->backSlashAtual = TAMANHO_GAP_ZOOM;
		}
	}else if(bufferIN->dadosSeprados.zoom < dadosZoom->lastRawValue){
		dadosZoom->backSlashAtual -= (dadosZoom->lastRawValue - bufferIN->dadosSeprados.zoom);
		if(dadosZoom->backSlashAtual < 0){
			dadosZoom->backSlashAtual = 0;
		}
	}
	dadosZoom->zoomCorrigido = bufferIN->dadosSeprados.zoom - dadosZoom->backSlashAtual;
	dadosZoom->lastRawValue = bufferIN->dadosSeprados.zoom ;

	pacoteFreed->Zoom  = dadosZoom->zoomCorrigido * REESCALA_ZOOM;
	if(pacoteFreed->Zoom > 65535){
		pacoteFreed->Zoom = 65535;
	}

#endif

	if(configGlobal.config.invEixos & INV_X){
		pacoteFreed->X *= -1;
	}
	if(configGlobal.config.invEixos & INV_Y){
		pacoteFreed->Y *= -1;
	}
	if(configGlobal.config.invEixos & INV_Z){
		pacoteFreed->Z *= -1;
	}
	if(configGlobal.config.invEixos & INV_PAN){
		pacoteFreed->Pan *= -1;
	}
	if(configGlobal.config.invEixos & INV_TILT){
		pacoteFreed->Tilt *= -1;
	}
	if(configGlobal.config.invEixos & INV_ROLL){
		pacoteFreed->Roll *= -1;
	}
	if(configGlobal.config.invEixos & INV_FOCO){
		pacoteFreed->Focus = 0xffff - pacoteFreed->Focus;
	}
	if(configGlobal.config.invEixos & INV_ZOOM){
		pacoteFreed->Zoom = 0xffff - pacoteFreed->Zoom;
	}


}

void CamDataToFreeD(uint8_t * bufferOut, bufferData * bufferCAM4, uint32_t CAM_ID, gap_zoom * dadosZoom,
	    dadosMapaTrack * mapaXY, dadosMapaTrack * mapaZ, dadosMapaTrack * mapaPAN, dadosMapaTrack * mapaROLL,
		float const vetdataXY[][3], float const vetdataZ[][2], float const vetdataPAN[][2], float const vetdataROLL[][2]){

	FreeD_D1_t estruturaFreed = {0};
	CamDataToEixos(&estruturaFreed, bufferCAM4, CAM_ID, dadosZoom, mapaXY, mapaZ, mapaPAN, mapaROLL, vetdataXY, vetdataZ, vetdataPAN, vetdataROLL);
	FreeD_D1_pack(bufferOut, 29, &estruturaFreed);
}

// Recebe o valor do track e tradus em uma rotação de pan, correspondente a rotação do trilho
float range180(float angulo){
    if(angulo > 180.0){
    	angulo -= 360.0;
    }else if(angulo < -180.0){
    	angulo = -360.0 - angulo;
    }
    return angulo;
}

// Executa a compensacao da pan devido a desalinhamentos do trilho.
void exec_mapa_1D(dadosMapaTrack * mapa, float trackData, float * offsetComp, float const vetdata[][2]){

	mapa->proxPosVet = -1;
	uint8_t achou = 0x00;
	if(mapa->vetSize != 0){											// Existe um mapa
		if(mapa->lastPoseVet != -1){								// Já foi usado, posicao anteiror diferente do default sem uso.
			if(vetdata[mapa->lastPoseVet][0] >= trackData) {	// valor anterior menor que o atual, voltando na tableta
				for(mapa->proxPosVet = mapa->lastPoseVet - 1; mapa->proxPosVet > -1; mapa->proxPosVet --){   		// varre o vetor de compensacao ate chegar na posicao igual ou menor ao da atual
					if(vetdata[mapa->proxPosVet][0] <= trackData){  			// achou a posicao do proximo ponto para fazer a compensacao. (vet[i] <= trackData <= vet[last])
						achou = 0xf0;
						if(mapa->proxPosVet < mapa->vetSize - 1){
							mapa->lastPoseVet = mapa->proxPosVet + 1;			// Faz com que o ultimo seja o ponto seguinte ao achado
						}
						break; // achou o ponto do mapa
					}
				}if(!achou){			// Caso nao tenha chada na varredura descendo, salva ultimo valor como a menor posicao do vetor
					mapa->lastPoseVet = 0;
				}
			}else{
				for(mapa->proxPosVet = mapa->lastPoseVet + 1; mapa->proxPosVet < mapa->vetSize; mapa->proxPosVet ++){   		// varre o vetor de compensacao ate chegar na posicao igual ou menor ao da atual
					if(vetdata[mapa->proxPosVet][0] >= trackData){  			// achou a posicao do proximo ponto para fazer a compensacao. (vet[i] <= trackData <= vet[last])
						achou = 0x0f;
						if(mapa->proxPosVet > 0){								// Faz com que o ultimo seja o ponto anterior ao achado
							mapa->lastPoseVet = mapa->proxPosVet - 1;
						}
						break; // achou o ponto do mapa
					}
				}if(!achou){				// Caso nao tenha chada na varredura subindo, salva ultimo valor como a maior posicao do vetor
					mapa->lastPoseVet = mapa->vetSize - 1;
				}
			}
		}else{
			for(mapa->proxPosVet = 0; mapa->proxPosVet < mapa->vetSize; mapa->proxPosVet++){
				if(vetdata[mapa->proxPosVet][0] >= trackData){
					if(mapa->proxPosVet < mapa->vetSize - 1){ // veririca que o ponto achado nao e o ultimo do vetor, e inicia a last com o ponto atual e o proximo com o proximo do mapa
						mapa->lastPoseVet = mapa->proxPosVet;
						mapa->proxPosVet++;
						achou = 0xf0;
					}else{
						mapa->lastPoseVet = mapa->proxPosVet; // Caso seja o ultimo ponto do vetor, fala que o proximo e o anteiror o ultimo ponto e o atual
						mapa->proxPosVet --;
						achou = 0x0f;
					}
					break;
				}
			}
			if(!achou){				// Caso nao tenha chada na varredura subindo, salva ultimo valor como a maior posicao do vetor
				mapa->lastPoseVet = mapa->vetSize - 1;
			}
		}
		if(mapa->proxPosVet != -1 && mapa->lastPoseVet != mapa->proxPosVet && achou){											// Ponto do mapa foi encontrado, calcula a equacao da reta

			mapa->distComp = (vetdata[mapa->lastPoseVet][0] - trackData) / (vetdata[mapa->lastPoseVet][0] - vetdata[mapa->proxPosVet][0]);

#ifdef 	COMP_BELZIER
			mapa->distComp = EaseBelzier(mapa->distComp);
#elif 	COMP_BELZIER_QUAD
			mapa->distComp = EaseBelzierN(mapa->distComp, EXP_BELZIER_N);
#elif	COMP_INOUT_QUAD
			mapa->distComp = EaseInOutQuad(mapa->distComp);
#elif	COMP_INOUT_CUB
			mapa->distComp = EaseInOutCubic(mapa->distComp);
#endif
			//(y2 - y1) *valor +
			*offsetComp = (vetdata[mapa->proxPosVet][1] - vetdata[mapa->lastPoseVet][1]) * mapa->distComp + vetdata[mapa->lastPoseVet][1];
		}
	}
}

void exec_mapa_2D(dadosMapaTrack * mapa, float trackData, float * vetComp, float const vetdata[][3]){
	mapa->proxPosVet = -1;
	uint8_t achou = 0x00;
	if(mapa->vetSize != 0){											// Existe um mapa
		if(mapa->lastPoseVet != -1){								// Já foi usado, posicao anteiror diferente do default sem uso.
			if(vetdata[mapa->lastPoseVet][0] >= trackData) {	// valor anterior menor que o atual, voltando na tableta
				for(mapa->proxPosVet = mapa->lastPoseVet - 1; mapa->proxPosVet > -1; mapa->proxPosVet --){   		// varre o vetor de compensacao ate chegar na posicao igual ou menor ao da atual
					if(vetdata[mapa->proxPosVet][0] <= trackData){  			// achou a posicao do proximo ponto para fazer a compensacao. (vet[i] <= trackData <= vet[last])
						achou = 0xf0;
						if(mapa->proxPosVet < mapa->vetSize - 1){
							mapa->lastPoseVet = mapa->proxPosVet + 1;			// Faz com que o ultimo seja o ponto seguinte ao achado
						}
						break; // achou o ponto do mapa
					}
				}if(!achou){			// Caso nao tenha chada na varredura descendo, salva ultimo valor como a menor posicao do vetor
					mapa->lastPoseVet = 0;
				}
			}else{
				for(mapa->proxPosVet = mapa->lastPoseVet + 1; mapa->proxPosVet < mapa->vetSize; mapa->proxPosVet ++){   		// varre o vetor de compensacao ate chegar na posicao igual ou menor ao da atual
					if(vetdata[mapa->proxPosVet][0] >= trackData){  			// achou a posicao do proximo ponto para fazer a compensacao. (vet[i] <= trackData <= vet[last])
						achou = 0x0f;
						if(mapa->proxPosVet > 0){								// Faz com que o ultimo seja o ponto anterior ao achado
							mapa->lastPoseVet = mapa->proxPosVet - 1;
						}
						break; // achou o ponto do mapa
					}
				}if(!achou){				// Caso nao tenha chada na varredura subindo, salva ultimo valor como a maior posicao do vetor
					mapa->lastPoseVet = mapa->vetSize - 1;
				}
			}
		}else{
			for(mapa->proxPosVet = 0; mapa->proxPosVet < mapa->vetSize; mapa->proxPosVet++){
				if(vetdata[mapa->proxPosVet][0] >= trackData){
					if(mapa->proxPosVet < mapa->vetSize - 1){ // veririca que o ponto achado nao e o ultimo do vetor, e inicia a last com o ponto atual e o proximo com o proximo do mapa
						mapa->lastPoseVet = mapa->proxPosVet;
						mapa->proxPosVet++;
						achou = 0xf0;
					}else{
						mapa->lastPoseVet = mapa->proxPosVet; // Caso seja o ultimo ponto do vetor, fala que o proximo e o anteiror o ultimo ponto e o atual
						mapa->proxPosVet --;
						achou = 0x0f;
					}
					break;
				}
			}
			if(!achou){				// Caso nao tenha chada na varredura subindo, salva ultimo valor como a maior posicao do vetor
				mapa->lastPoseVet = mapa->vetSize - 1;
			}
		}
		if(mapa->proxPosVet != -1 && mapa->lastPoseVet != mapa->proxPosVet && achou){											// Ponto do mapa foi encontrado, calcula a equacao da reta

			mapa->distComp = (vetdata[mapa->lastPoseVet][0] - trackData) / (vetdata[mapa->lastPoseVet][0] - vetdata[mapa->proxPosVet][0]);

#ifdef 	COMP_BELZIER
			mapa->distComp = EaseBelzier(mapa->distComp);
#elif 	COMP_BELZIER_QUAD
			mapa->distComp = EaseBelzierN(mapa->distComp, EXP_BELZIER_N);
#elif	COMP_INOUT_QUAD
			mapa->distComp = EaseInOutQuad(mapa->distComp);
#elif	COMP_INOUT_CUB
			mapa->distComp = EaseInOutCubic(mapa->distComp);
#endif
			// Calcula o fator de multiplicacao para a compenscacao que sera proporcional a posicao do track na equacao da reta.
			vetComp[0] = (vetdata[mapa->proxPosVet][1] - vetdata[mapa->lastPoseVet][1]) * mapa->distComp + vetdata[mapa->lastPoseVet][1];
			vetComp[1] = (vetdata[mapa->proxPosVet][2] - vetdata[mapa->lastPoseVet][2]) * mapa->distComp + vetdata[mapa->lastPoseVet][2];

		}
	}
}

void printSetup(void){
   /* Serial.println("*****************PARAMETROS DO TRILHO ******************");
    Serial.println("X0C1: " + String(X0C1) + " Y0C1: " + String(Y0C1));
    Serial.println("X0C2: " + String(X0C2) + " Y0C2: " + String(Y0C2));
    Serial.println("RAIO1: " + String(RAIO1) + " RAIO2: " + String(RAIO2));
    Serial.println("TETHA0_R1: " + String(TETHA0_R1) + " TETHA0_R2: " + String(TETHA0_R2));
    Serial.println("DELTA_X_R1_R2: " + String(DELTA_X_R1_R2) + " DELTA_Y_R1_R2: " + String(DELTA_Y_R1_R2));
    Serial.println("**********************************************************");
    */
}

float EaseBelzier(float val){
    float sqt = val * val;
    return sqt / (2.0 * (sqt - val) + 1.0);
}

float EaseBelzierN(float val, float alpha){
    if (val < 0){
    	val = 0;
    }
    float sqt = powf(val, alpha);
    return sqt / (sqt + powf(1 - val, alpha));
}

float EaseInOutQuad(float val){
    if (val <= 0.5){
    	return 2.0 * val * val;
    }
    val -= 0.5;
    return 2.0 * val * (1.0 - val) + 0.5;
}

float EaseInOutCubic(float val){
    if (val <= 0.5){
    	return 2.0 * powf(val,3);
    }
    val -= 0.5;
    return 2.0 * val * (1.0 - val) + 0.5;
}
