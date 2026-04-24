# realtime-event-api

> RTSP 기반 영상 데이터를 실시간 이벤트로 변환하는 시스템  
> AI와 협업하여 설계 및 구현된 Industrial Monitoring API

---

## Vision

이 프로젝트는 단순한 영상 처리 시스템이 아니라,  
**AI와 사람이 함께 시스템을 만들어가는 방식**을 실험하기 위해 시작되었습니다.

개발 과정에서 AI는 코드 생성 도구가 아니라  
설계, 검증, UI, 구조 개선까지 함께 수행하는 **협업 파트너**로 활용되었습니다.

저는 그 결과들을 연결하고,  
실제 동작하는 시스템으로 통합하는 역할을 수행했습니다.

앞으로 개발은 특정 개인의 역량이 아니라  
**AI와 협업하는 방식으로 확장되는 방향으로 변화할 것**이라고 생각합니다.

이 프로젝트는 그 흐름을 기반으로 만들어졌습니다.

---

## Overview

RTSP 카메라 스트림을 기반으로  
특정 영역(ROI)에서 발생하는 변화를 감지하고

이를 **이벤트 → 상태 → API → UI** 흐름으로 변환하는 시스템입니다.

운영자는 웹 UI를 통해  
카메라 상태, 이벤트, ROI 설정을 실시간으로 제어할 수 있습니다.

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

Architecture
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
API 중심 구조
Runtime / Application 분리
외부 AI 서비스와 유연한 연동 구조

Key Features
RTSP 기반 실시간 영상 처리
ROI 기반 이벤트 감지
생산 이벤트 카운팅 및 상태 관리
SignalR 기반 실시간 상태 전송
카메라 제어 API (Start / Stop / Status)
ROI 디버그 및 설정 UI
OCR 기반 라벨 검증 (Python 서비스 연동)
AI Collaboration

이 시스템은 다음과 같은 AI 도구들과 협업하여 개발되었습니다.

ChatGPT
시스템 설계 및 문제 해결
Codex
코드 생성 및 리팩터링
Gemini
UI 구조 및 UX 개선
NanoBanana
로고 및 디자인 생성
v0 (Vercel)
UI 프로토타이핑 및 레이아웃 설계
Human Role
CHANWOOK JEONG (Korea)
System Orchestrator
시스템 방향 조율
AI 결과 검토 및 통합
실제 동작 가능한 구조로 연결

AI tools were used as engineering partners, not as shortcuts.
Human judgment connected the AI-assisted outputs into one working system.

Tech Stack
Backend: ASP.NET Core
Vision: OpenCvSharp
Realtime: SignalR
Database: MSSQL (EF Core + Dapper)
Frontend: HTML / JavaScript
External Dependencies
MediaMTX (RTSP Relay)
RTSP 스트림 중계
실행 파일 필요
Realtime Vision Service
OCR 기반 ROI 라벨 검증
Python FastAPI 서비스

https://github.com/echo783/realtime-vision-service

What This Project Represents

이 프로젝트는 단순한 기능 구현이 아니라,

실시간 데이터 흐름 설계
장비 기반 시스템 구성
AI 협업 기반 개발 방식

을 실제로 구현한 결과입니다.

핵심은 코드 자체가 아니라
시스템을 구성하고 연결하는 과정입니다.

Future Direction
Camera Runtime 인터페이스 분리
Detection 모듈 구조화
이벤트 처리 고도화
AI 분석 기능 확장 (LLM 기반 운영 분석)
실시간 시스템 아키텍처 고도화

## Quick Start (Database)

```MSSQL

ProductVersion : 17.0.1000.7
ProductLevel : RTM
Edition : Express Edition (64-bit)
create DB : FactoryDB
docs/sql/schema-and-seed.sql 실행
 




