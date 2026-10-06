/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.h
  * @brief          : Header for main.c file.
  *                   This file contains the common defines of the application.
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
/* USER CODE END Header */

/* Define to prevent recursive inclusion -------------------------------------*/
#ifndef __MAIN_H
#define __MAIN_H

#ifdef __cplusplus
extern "C" {
#endif

/* Includes ------------------------------------------------------------------*/
#include "stm32f4xx_hal.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */

/* USER CODE END Includes */

/* Exported types ------------------------------------------------------------*/
/* USER CODE BEGIN ET */

/* USER CODE END ET */

/* Exported constants --------------------------------------------------------*/
/* USER CODE BEGIN EC */

/* USER CODE END EC */

/* Exported macro ------------------------------------------------------------*/
/* USER CODE BEGIN EM */

/* USER CODE END EM */

/* Exported functions prototypes ---------------------------------------------*/
void Error_Handler(void);

/* USER CODE BEGIN EFP */

/* USER CODE END EFP */

/* Private defines -----------------------------------------------------------*/
#define TRIGGER_SYNC_Pin GPIO_PIN_15
#define TRIGGER_SYNC_GPIO_Port GPIOC
#define TRIGGER_SYNC_EXTI_IRQn EXTI15_10_IRQn
#define BT_LOAD_DEFAULT_Pin GPIO_PIN_0
#define BT_LOAD_DEFAULT_GPIO_Port GPIOA
#define SOFT_RST_ETH_Pin GPIO_PIN_1
#define SOFT_RST_ETH_GPIO_Port GPIOA
#define SOFT_CS_ETH_Pin GPIO_PIN_4
#define SOFT_CS_ETH_GPIO_Port GPIOA
#define LED_ERROR_HANDLE_Pin GPIO_PIN_0
#define LED_ERROR_HANDLE_GPIO_Port GPIOB
#define LED_RX_UART_OK_Pin GPIO_PIN_1
#define LED_RX_UART_OK_GPIO_Port GPIOB
#define LED_STATUS_BOOTING_Pin GPIO_PIN_2
#define LED_STATUS_BOOTING_GPIO_Port GPIOB
#define SYNC_OK_STATUS_Pin GPIO_PIN_10
#define SYNC_OK_STATUS_GPIO_Port GPIOB
#define LED_STATUS_TX_UDP_Pin GPIO_PIN_9
#define LED_STATUS_TX_UDP_GPIO_Port GPIOA
#define KNOB_A_Pin GPIO_PIN_5
#define KNOB_A_GPIO_Port GPIOB
#define KNOB_B_Pin GPIO_PIN_6
#define KNOB_B_GPIO_Port GPIOB
#define KNOB_PUSH_Pin GPIO_PIN_7
#define KNOB_PUSH_GPIO_Port GPIOB

/* USER CODE BEGIN Private defines */

/* USER CODE END Private defines */

#ifdef __cplusplus
}
#endif

#endif /* __MAIN_H */
