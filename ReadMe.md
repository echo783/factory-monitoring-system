## realtime-event-api
## What is this?

RTSP 카메라 데이터를 실시간 이벤트로 변환하고  
웹에서 상태를 확인할 수 있는 Industrial Monitoring API입니다.

실시간 카메라 → 이벤트 → 상태 → 웹 UI까지 한 번에 연결되는 구조를 보여주는 프로젝트입니다.

## Why?

기존 장비 중심 시스템에서 상태 공유가 어렵고  
실시간 이벤트 흐름이 분산되는 문제를 해결하기 위해 만들었습니다.

## 포트폴리오
- docs/myport.pdf

## Demo
<img width="1317" height="917" alt="image" src="https://github.com/user-attachments/assets/00e50490-b59f-447a-81dd-f65ae6bcea7a" />

## Vision

이 프로젝트는 단순한 영상 처리 시스템이 아니라,
물리 데이터(RTSP)를 이벤트와 상태로 변환하는 시스템 설계를 목표로 합니다.

개발 과정에서 AI는 코드 생성 도구가 아니라
설계 검토, 구조 개선, UI 흐름까지 함께 수행하는 협업 파트너로 활용되었습니다.

---

## Overview

RTSP 카메라 스트림을 기반으로
특정 영역(ROI)의 변화를 감지하고

이를 다음 흐름으로 변환합니다.

Event → State → API → UI

운영자는 웹 UI를 통해
카메라 상태와 이벤트를 실시간으로 확인하고 제어할 수 있습니다.

---

## Core Flow

```text
RTSP Stream
   ↓
Frame Processing (OpenCV)
   ↓
ROI Detection
   ↓
Event Generation
   ↓
State Management
   ↓
API (ASP.NET Core)
   ↓
Web UI (Realtime Dashboard)
```

## Architecture
```text
Controller → Application → Infrastructure
                      ↓
               Camera Runtime
                      ↓
                    MSSQL

Application → External Service (HTTP)
                      ↓
         Realtime Vision Service (OCR)
```
## 설계 포인트
API 중심 구조
Runtime / Application 책임 분리
외부 AI 서비스와 유연한 연동 구조

## Camera Runtime Design

카메라 실행 상태는 단일 runner ownership 모델로 관리됩니다.

CameraRuntimeRegistry
→ cameraId별 runner 상태 관리
CameraOrchestrator
→ DB 기준 자동 실행 동기화 (BackgroundService)
CameraRuntimeController
→ 수동 Start / Stop 처리
CameraRuntimeSessionLifecycle
→ runner Stop / Dispose 책임
CameraRuntimeStatusNotifier
→ SignalR 상태 전파 책임

핵심 설계
TrySetRunner → 실행 소유권 획득
TryTakeRunner → 종료 소유권 회수

이를 통해

중복 실행 방지
종료 경합 방지
상태 일관성 유지

를 해결했습니다.
## Key Features
영상 데이터를 이벤트로 변환하는 구조
ROI 기반 변화 감지를 통한 이벤트 생성
이벤트 → 상태 → API 흐름 구조화
SignalR 기반 실시간 상태 공유
외부 OCR 서비스 연동을 통한 라벨 검증

## AI Collaboration

AI는 코드 생성 도구가 아니라
설계와 검증을 함께 수행하는 협업 파트너로 활용되었습니다.

AI 역할
구조 설계 검토
코드 생성 및 리팩터링
UI 흐름 개선
Human Role – CHANWOOK JEONG
시스템 구조 설계
AI 결과 검증 및 통합
전체 시스템 완성
Engineering Insight

AI는 개발의 시작을 빠르게 만들었습니다.

하지만

구조를 설계하고
시스템을 연결하고
실제로 동작하게 만드는 과정은

여전히 사람의 역할입니다.

AI는 도구가 아니라,
함께 문제를 해결하는 파트너입니다.

## Tech Stack
Backend: ASP.NET Core
Vision: OpenCvSharp
Realtime: SignalR
Database: MSSQL (EF Core + Dapper)
Frontend: HTML / JavaScript
External Dependencies
MediaMTX
→ RTSP 스트림 중계
Realtime Vision Service
→ OCR 기반 ROI 라벨 검증
https://github.com/echo783/realtime-vision-service
What This Project Represents

이 프로젝트는

실시간 데이터 흐름을 설계하고
물리 데이터를 이벤트와 상태로 변환하며
시스템 전체를 하나의 구조로 연결하는 과정입니다.

핵심은 코드가 아니라

데이터를 흐름으로 만들고
흐름을 시스템으로 완성하는 것

입니다.

## Future Direction
Camera Runtime 인터페이스 고도화
Detection 모듈 구조화
이벤트 처리 로직 확장
LLM 기반 운영 분석 기능 추가
실시간 아키텍처 고도화

## Quick Start (Database)

 1. MSSQL
- ProductVersion : 17.0.1000.7
- ProductLevel : RTM
- Edition : Express Edition (64-bit)
- create DB : FactoryDB
- docs/sql/schema-and-seed.sql 실행

 2. 서버 실행
 3. /lognin.html 접속
 4. admin / 1234


