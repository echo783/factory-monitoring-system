# Factory Monitoring System

> RTSP 기반 영상 데이터를 실시간 이벤트로 변환하여 API로 제공하는 시스템

RTSP 기반 실시간 영상 분석 및 이벤트 처리 시스템  
(C#, ASP.NET Core, SignalR, OpenCV, MSSQL)

---

## Overview

RTSP 카메라 스트림을 분석하여  
특정 영역(ROI)에서 발생하는 이벤트를 감지하고  
이를 **실시간 상태 데이터로 변환하여 API로 제공하는 시스템**입니다.

운영자는 웹 UI를 통해  
카메라 제어, 상태 모니터링, ROI 설정을 실시간으로 수행할 수 있습니다.

---

## Key Features

- RTSP 영상 기반 실시간 이벤트 감지
- ROI 기반 객체 변화 및 라벨 진입 판단
- 생산 이벤트(Count) 생성 및 상태 관리
- 카메라 제어 API (Start / Stop / Status)
- SignalR 기반 실시간 상태 전송
- ROI 설정 및 디버그 기능
- JWT 기반 인증 및 보호된 API

---

## Architecture

```text
Controller → Application → Infrastructure
                      ↓
               Camera Runtime (OpenCV)
                      ↓
                  MSSQL
```
- API 중심 구조로 웹 UI 및 외부 시스템과 확장 가능
- 런타임 영상 처리와 비즈니스 로직 분리
- DTO 기반 요청/응답 모델 분리

---

## Tech Stack

- Backend: ASP.NET Core
- Vision: OpenCvSharp (OpenCV)
- Communication: SignalR
- Database: MSSQL (EF Core + Dapper)
- Frontend: HTML / JavaScript

---

## Technical Highlights

- OpenCV 기반 프레임 변화량 분석 로직 직접 구현
- ROI 기반 이벤트 감지 구조 설계
- CameraOrchestrator 기반 런타임 세션 관리
- EF Core + Dapper 혼합 구조로 성능과 생산성 균형
- 실시간 데이터 처리 및 상태 관리 구조 설계

---

## Web UI

- 로그인 (JWT 인증)
- 카메라 상태 조회 및 제어
- 실시간 상태 모니터링
- ROI 디버그 페이지 (좌표 수정 및 저장)

---

## Project Structure
```text
FactoryApi
├─ Controllers
├─ Application
├─ Contracts
├─ Infrastructure
│ ├─ CameraRuntime
│ ├─ Persistence
│ └─ DependencyInjection
├─ Models
└─ wwwroot

docs
└─ Portfolio 자료
```


---

## What I Focused On

- 실시간 영상 데이터를 **이벤트 기반 상태로 변환하는 구조 설계**
- API 중심 아키텍처로 확장성과 유지보수성 확보
- 장비(RTSP 카메라) 연동 기반 시스템 구현 경험
- Runtime / Application / Infrastructure 계층 분리

---

## Future Improvements

- Camera Runtime 인터페이스화 (ICameraRuntimeManager)
- Detection 모듈 분리
- Query / Command 구조 개선
- Logging 및 모니터링 구조 강화
- Domain 구조 개선

---

## External Dependency (MediaMTX)

본 프로젝트는 RTSP 스트림 처리를 위해 MediaMTX가 필요합니다.
아래 경로에 실행 파일을 위치시켜 주세요:
FactoryApi/tools/mediamtx/

필수 파일:
- mediamtx.exe

다운로드:
https://drive.google.com/file/d/1MmPli1E5Jfl-LgpTdv6rewxB1XXXY1VW/view?usp=drive_link

테스트 URL
rtsp://admin:chan1324!@cksdnr7979223.iptime.org:8554/Streaming/Channels/101

※ 실행 전 mediamtx.exe가 해당 경로에 존재해야 정상적으로 카메라 스트림이 동작합니다.
---

## Portfolio

- Factory Monitoring Portfolio PDF  
  docs/JeongChanwook_Factory_Monitoring_Portfolio.pdf



