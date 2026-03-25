Factory Monitoring System

# 프로젝트 개요
공장 설비 및 카메라(RTSP)를 활용하여
생산 상태를 분석하고 모니터링하는 시스템입니다.

물체 1회전 시 라벨이 인식되면  
제품 1개 생산으로 판단하는 로직으로 구성하였습니다.

# 구성
- Backend: ASP.NET Core API
- Client: WPF Desktop
- Camera: RTSP
- DB: MSSQL

# 사용 기술 및 선택
- RTSP (Real Time Streaming Protocol)  
  = 공장 카메라 영상 스트리밍을 처리하기 위해 사용

- OpenCV  
  = 영상 분석 및 라벨 인식 기반 생산량 카운트 구현

- ASP.NET Core API  
  = 클라이언트와 통신 및 데이터 처리

- WPF (MVVM 패턴)  
  = 실시간 모니터링 UI 구성

- MSSQL  
  = 생산 / 재고 / 출고 데이터 관리

- Dapper  
  = 단순 조회 및 성능이 중요한 쿼리 처리에 사용

- EF Core  
  = 데이터 삽입 및 복잡한 로직 처리에 사용


# 실행 화면
- 메인화면 : images/main.png
- roi 분석 : images/ROI.png
- 납품 등록 : images/납품등록.png
- 납품 조회 : images/납품조회.png
- 생산 조회 : images/생산조회.png

# 주요 기능
- RTSP 영상 분석
- 생산량 자동 카운트
- 이미지 캡처 저장
- 실시간 상태 모니터링
- CRUD / MVVM 기반 관리 기능

# 핵심 기술 포인트
- OpenCV 기반 영상 분석
- 상태 기반 카운트 로직 구현
- API ↔ WPF 실시간 연동

# 프로젝트 구조
- FactoryApi : API 서버
- FactoryClient : WPF 클라이언트

# Database

본 시스템은 생산 - 재고 - 출고 흐름을 관리하는 구조로 설계되었습니다.

# 주요 흐름
카메라(RTSP) 기반으로 생산 이벤트를 감지하고,  
생산된 제품은 재고로 관리되며,  
출고 시 StockOutHistory에 기록됩니다.

# 테이블 구성

- CameraConfig  
  = 카메라 설정 및 RTSP 주소 관리

- ProductionEvent  
  = 카메라를 통해 감지된 생산 이벤트 기록

- Inventory  
  = 생산된 제품의 현재 재고 수량 관리

- Delivery  
  = 납품처 정보 관리

- DeliveryItem  
  = 납품 요청 및 수량 정보

- StockOutHistory  
  = 실제 출고 이력 기록  
  (어떤 제품이 언제, 얼마만큼 출고되었는지 추적 가능)

# 데이터 흐름

카메라를 통해 ProductionEvent로 생산을 감지하고,
실제 재고는 포장 완료 이후 Inventory에 등록하도록 분리했습니다.
이후 납품 요청이 발생하면 DeliveryItem을 통해 관리하고,
최종 출고는 StockOutHistory에 기록되도록 설계했습니다

