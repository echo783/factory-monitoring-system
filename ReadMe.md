# Factory Monitoring System

RTSP 기반 실시간 데이터 분석 및 API 연동 시스템
(C#, ASP.NET Core, WPF, OpenCV, MSSQL)

영상 데이터를 기반으로 이벤트를 감지하고,
API 및 클라이언트와 연동하여 실시간 상태를 처리하는 시스템입니다.

---

## 프로젝트 개요

본 프로젝트는 RTSP 영상 데이터를 기반으로
이벤트를 감지하고 상태를 생성하여 API와 클라이언트로 전달하는
실시간 데이터 처리 시스템입니다.

특정 영역(ROI) 내 객체 변화 및 라벨 진입 이벤트를 기준으로
상태를 판단하고, 이를 실시간으로 전달하는 구조로 설계하였습니다.

또한 SignalR과 CameraOrchestrator를 활용하여
클라이언트에서 분석 세션을 실시간으로 제어할 수 있도록 구성하였습니다.

---

## 시스템 구성

* **Backend**: ASP.NET Core API
* **Client**: WPF Desktop (MVVM)
* **Vision**: OpenCV
* **Communication**: SignalR
* **Database**: MSSQL

---

## 핵심 기능

* RTSP 영상 기반 이벤트 감지
* 상태 기반 데이터 생성 및 처리
* 실시간 상태 모니터링 및 제어
* API 기반 데이터 관리 (CRUD)
* 외부 시스템 연동 가능한 구조 설계

---

## 핵심 기술 포인트

* OpenCV 기반 이벤트 감지 로직 구현
* ROI 기반 분석 영역 설정 및 처리
* CameraOrchestrator 기반 세션 관리 구조
* SignalR 기반 실시간 데이터 전달
* API 중심 아키텍처 설계
* 클라이언트-서버 간 상태 동기화 구조 구현

---

## 사용 기술

* RTSP (영상 스트리밍)
* OpenCV (영상 분석)
* ASP.NET Core API (데이터 처리 및 통신)
* SignalR (실시간 통신)
* WPF (MVVM 기반 클라이언트 UI)
* MSSQL (데이터 저장)
* Dapper / EF Core (데이터 접근)

---

## 실행 화면

(포트폴리오 PDF 참고)

---

## 프로젝트 구조

* **FactoryApi** : ASP.NET Core API 서버
* **FactoryClient** : WPF 클라이언트
* **docs** : 문서 및 포트폴리오 자료

---

## 기술적 특징

* 실시간 데이터 처리 및 상태 기반 로직 구현
* API 중심 구조 설계 및 클라이언트 연동
* 외부 장비/데이터 연동을 고려한 확장 가능한 구조
* 영상 분석을 활용한 이벤트 기반 처리 시스템 구현

---

## 운영 웹 및 인증 기능

* JWT 기반 인증 및 운영자 로그인 기능 구현
* 보호된 API([Authorize])를 통해 카메라 제어 및 데이터 접근 제한
* 웹 기반 운영 페이지(index.html)를 통해 카메라 시작/중지/상태 조회 기능 제공
* ROI 디버그 페이지를 통해 분석 영역을 실시간으로 수정 및 저장
* 인증 상태에 따라 접근 제어 및 세션 만료 처리 구현

---
## Portfolio

* [Factory Monitoring Portfolio PDF](docs/JeongChanwook_Factory_Monitoring_Portfolio.pdf)
