# Factory Monitoring System

RTSP 기반 공장 생산 모니터링 시스템 (C# API + WPF + OpenCV + MSSQL)

RTSP 기반 영상 분석을 통해 생산량을 자동 감지하고  
재고 및 출고 흐름까지 관리하는 통합 시스템입니다.

---

## 프로젝트 개요

공장 설비 및 카메라(RTSP)를 활용하여  
생산 상태를 분석하고 모니터링하는 시스템입니다.

물체가 1회전하면서 라벨이 인식되면  
제품 1개 생산으로 판단하는 로직으로 구성하였습니다.

SignalR과 CameraOrchestrator를 연계하여  
WPF 클라이언트에서 카메라 분석 세션의 Start/Stop을  
실시간으로 제어할 수 있도록 구현하였습니다.

---

## 구성

- Backend: ASP.NET Core API  
- Client: WPF Desktop (MVVM)  
- Camera: RTSP  
- DB: MSSQL  

---

## 사용 기술 및 선택 이유

RTSP (Real Time Streaming Protocol)  
공장 카메라 영상 스트리밍 처리

OpenCV  
영상 분석 및 라벨 인식 기반 생산량 카운트 구현

ASP.NET Core API  
클라이언트와 통신 및 데이터 처리

WPF (MVVM 패턴)  
실시간 모니터링 UI 구성

MSSQL  
생산 / 재고 / 출고 데이터 관리

Dapper  
단순 조회 및 고성능 쿼리 처리

EF Core  
데이터 삽입 및 복잡한 비즈니스 로직 처리

---

## 실행 화면

- 메인 화면 : images/main.png
- ROI 분석 : images/ROI.png
- 납품 등록 : images/납품등록.png
- 납품 조회 : images/납품조회.png
- 생산 조회 : images/생산조회.png

---

## 주요 기능

- RTSP 영상 분석
- 생산량 자동 카운트
- 이미지 캡처 저장
- 실시간 상태 모니터링
- 카메라 Start / Stop 제어
- CRUD 기반 관리 기능

---

## 핵심 기술 포인트

- OpenCV 기반 영상 분석
- 상태 기반 생산 카운트 로직 구현
- CameraOrchestrator 기반 세션 관리
- SignalR을 통한 실시간 제어 및 상태 동기화
- API와 WPF 간 실시간 연동 구조

---

## 프로젝트 구조

- FactoryApi : ASP.NET Core API 서버  
- FactoryClient : WPF 클라이언트  
- docs : 문서 및 이미지  

---

## 데이터베이스 설계

본 시스템은 생산 → 재고 → 출고 흐름을 기준으로 설계되었습니다.

### 주요 테이블

CameraConfig  
카메라 설정 및 RTSP 주소 관리  

ProductionEvent  
카메라를 통해 감지된 생산 이벤트 기록  

Inventory  
생산된 제품의 현재 재고 수량 관리  

Delivery  
납품처 정보 관리  

DeliveryItem  
납품 요청 및 수량 정보  

StockOutHistory  
실제 출고 이력 기록  
출고 제품, 시간, 수량 추적 가능  

---

## 데이터 흐름

카메라(RTSP)를 통해 생산 이벤트를 감지하고  
ProductionEvent로 저장합니다.

생산 완료된 제품은 Inventory에 반영합니다.

납품 요청은 DeliveryItem으로 관리하며  
최종 출고 시 StockOutHistory에 기록됩니다.

생산과 재고 반영을 분리하여  
현장 프로세스를 반영한 구조로 설계하였습니다.

---

## 특징

카메라 기반 생산 자동화 시스템 구현  
실시간 제어 및 상태 반영  
영상 분석과 재고/출고 시스템 통합  
확장 가능한 구조