# JahwaInspector.Light 작업 현황

최종 갱신: 2026-07-22

## 프로젝트 개요

- .NET 8 WPF 기반 측정 데이터 분석 도구
- 지원 검사: FRA, DC SENSITIVITY, SCAN, SETTLING TIME
- 주요 출력: 요약 CSV, Excel, 그래프 이미지

## 현재까지 구현된 내용

- FRA CSV 분석 및 Gain Margin, Phase Margin 등 계산
- DC Sensitivity 데이터 분석 및 결과 내보내기, `AKM`/`DW` Data Type 지원(SCAN과 동일한 Registry 패턴, `Services/DcSensitivityDataTypes.cs`)
- SCAN 데이터 분석과 결과 요약
- SCAN 데이터 형식으로 `AKM`, `robocube` 지원
- 데이터 형식별 CSV 읽기, Hysteresis/Linearity 계산 및 기준선 생성
- robocube 데이터의 실제 Target 최솟값/최댓값 기반 자동 범위 처리
- SCAN 요약 CSV, Excel, 그래프 이미지 및 오류 로그 출력
- ScottPlot 기반 FRA/DC/SCAN 그래프 표시
- SCAN 데이터 형식에 따른 그래프 축과 눈금 처리
- SETTLING TIME 분석: `DW`, `Robocube`, `K-star`, `Anosys` 4개 Data Type 지원
  - Data Type별 CSV/xlsx 파서(`Services/SettlingCsvReader.cs`), Settling Time/Overshoot 계산(`Services/SettlingMetrics.cs`)
  - 조건(그룹)별 Tolerance Band 개별 지정, Tail Window(%) 실시간 재계산
  - 그룹별 서브플롯 + 전체 그리드 보기, `Settling_Summary.csv`/조건별 PNG 내보내기, Judge(NG 사유) 표시

## 최근 확인된 작업

- 2026-07-22: DC SENSITIVITY에 SCAN과 동일한 Data Type 방식(`AKM`, `DW`) 도입 및 실제 샘플 CSV로 검증
- 2026-07-22: SETTLING TIME에 `Anosys` Data Type 추가 및 실제 샘플 CSV로 검증
- 2026-07-16~07-20: SETTLING TIME 기능 신규 구현(DW/Robocube/K-star), 이 문서에는 미반영 상태였음(아래 변경 이력에 소급 정리)
- 2026-07-13: FRA 설정과 CSV 헤더 안내를 하나의 미니멀 카드로 통합
- 2026-07-13: 왼쪽 하단 Output/저장 경로 텍스트 제거
- 2026-07-13: FRA 설정 UI를 카드형 레이아웃으로 정리
- 2026-07-13: FRA 모드/Phase를 2열 배치하고 Data Type과 CSV 헤더 안내의 시각적 계층 개선
- 2026-07-13: FRA의 `IC Vendor` 선택을 `Data Type` 선택으로 변경
- 2026-07-13: FRA Data Type별 Frequency/Gain/Phase 검색 헤더를 화면에 표시
- 2026-07-13: FRA 헤더 정의를 `FraDataTypeRegistry`로 통합
- 2026-07-12: `robocube` SCAN 형식과 전용 Reader/Metrics 추가
- 2026-07-12: SCAN 형식 Registry 및 설정 화면 연동
- 2026-07-12: SCAN 그래프 범위와 축 눈금 처리 수정
- 2026-07-12: `MainViewModel.cs`, `PlotView.xaml.cs` 최종 수정 및 빌드 결과 생성

## 현재 확인이 필요한 사항

- 작업공간에는 판별에 사용할 실제 FRA 샘플 CSV가 없음
- 실제 AKM/robocube 샘플 CSV를 사용한 결과값 검증
- 저장된 CSV, Excel, 그래프 이미지의 형식과 수치 확인
- 현재 셸에서 Git 실행 파일을 찾을 수 없어 커밋 이력은 확인하지 못함(이 프로젝트는 Git 저장소가 아님)
- **SETTLING TIME 공통 이슈**: 기본 Tolerance Band(100)가 Robocube/Anosys 실측 데이터의 변위 스케일(수~수십 단위)보다 훨씬 커서, 기본값 그대로면 대부분 SettlingTime=0ms로 계산됨. 조건별 Tolerance Band(`SettlingSpec.ConditionTolerances`)를 실측 변위에 맞게 조정해야 정상적인 값이 나옴 — 값 자체는 정상 계산되나 기본 파라미터 튜닝이 필요
- SETTLING TIME - DW: Reader/UI는 구현되어 있으나 실제 DW 샘플로 한 번도 Export를 돌려본 산출물이 없음(미검증)
- SETTLING TIME - Anosys: `AF/Settling/Anosys` 샘플 CSV로 파싱 로직 검증 완료(아래 07-22 항목 참조). 채널 1,2=Position(위치), 3,4=Current(전류)로 사용자 확인 완료 — Current는 K-star가 Current 컬럼을 무시하는 것과 동일하게 Settling 판정에서 완전히 제외(파싱 자체를 안 함), Position1/Position2만 그룹으로 판정됨. 다만 `settling.csv` 파일이 `ANOSYS_SETTLING_MD21512B0_1.csv`와 완전히 동일한 내용이라 폴더째 Import하면 동일 데이터가 중복 집계됨 — 실사용 전 정리 필요
- SETTLING TIME - AKM: 샘플 데이터 폴더만 존재, Reader/UI 미구현
- DC SENSITIVITY - AKM/DW: Data Type Registry 도입 완료, 실측 샘플로 Up/Down 파싱 검증 완료(아래 07-22 (2) 항목 참조)
- DC SENSITIVITY - Anosys/Robocube: `SW DATA/AF|OIS/DC` 아래 폴더만 존재하고 샘플 CSV가 없어 Data Type 미구현

## 다음 작업

- 실제 AKM/DW 샘플 CSV를 사용해 확정한 헤더 규칙 검증(FRA)
- SETTLING TIME: Robocube/Anosys 기본 Tolerance Band 값 재산정, DW 실측 검증, `settling.csv` 중복 파일 정리

## 작업 기록 규칙

작업할 때마다 아래 항목을 이 문서에 갱신한다.

1. 변경 날짜와 요청 목적
2. 수정한 기능 및 주요 파일
3. 실행한 빌드·테스트와 결과
4. 남은 문제와 다음 작업

## 변경 이력

### 2026-07-22 (2) — DC SENSITIVITY Data Type 도입

- 목적: DC Sensitivity도 SCAN처럼 Data Type을 정해두고 각각 적용해달라는 요청 대응
- 배경 확인: 기존 DC Sensitivity는 Data Type 구분 없이 사용자가 직접 입력하는 Input/Output 후보명 텍스트(`DcSensitivitySpec.InputCandidatesText/OutputCandidatesText`)로만 헤더를 찾는 단일 로직이었음. `SW DATA/AF|OIS/DC` 아래 `AKM`,`Anosys`,`DW`,`Robocube` 폴더가 있었으나 실제 샘플은 `AKM`, `DW`에만 존재(Anosys/Robocube DC는 빈 폴더) — 사용자 확인 후 AKM/DW 2개만 우선 구현하기로 결정
- AKM: 기존 로직과 동일(Input 후보 `position code [dec]` 등, Output 후보 `output` 등, 자유 헤더 탐색)
- DW: 사용자 확인에 따라 Input=`DAC`, Output=`Position_0`로 고정. `Loop mode/Start Target/I2C Read/Delay/Step Target` key:value preamble 뒤에 헤더가 오는 구조라 기존 `DcSensitivityCsvReader`의 헤더 자동 탐지 로직을 그대로 재사용(별도 파서 불필요)
- 신규 파일: `Services/DcSensitivityDataTypes.cs` — SCAN의 `IScanDataTypeHandler`/`ScanDataTypeRegistry`와 동일한 구조로 `IDcSensitivityDataTypeHandler`/`DcSensitivityDataTypeRegistry` 도입(`AkmDcSensitivityDataTypeHandler`, `DwDcSensitivityDataTypeHandler`)
- 수정 파일: `ViewModels/SpecViewModels.cs`(`DcSensitivitySpecViewModel`에 `DataTypeOptions`/`SelectedDataType`/`ConfiguredInputHeader`/`ConfiguredOutputHeader`/`ConfiguredHeaderDescription` 추가), `ViewModels/MainViewModel.cs`(`ExportDcSensitivityBatch`가 `DcSensitivityDataTypeRegistry.Get(...).Read(...)` 사용하도록 변경), `App.xaml`(DC Sensitivity DataTemplate에 SCAN과 동일한 Data Type 콤보박스 + Rule/Input/Output 안내 카드 추가)
- 빌드: 앱이 실행 중이라(PID 20692 → 이후 PID 34132) 산출물 파일이 잠겨 있어 사용자 승인 받아 프로세스 종료 후 `dotnet build --no-restore` 진행, 결과 오류 0개(기존 SkiaSharp NU1701 경고만)
- 검증: 앱과 별개로 실제 소스를 참조하는 임시 콘솔 하네스(스크래치 폴더, 작업 후 삭제)로 실제 샘플 3건 실행
  - AKM(AF), AKM(OIS): `Cav1_#1_result_scan.csv` — HeaderRow=3, Input=`position code [dec]`, Output=`output`, Up 61개(0→4080)/Down 60개(4012→0), Output 범위 0~4095로 정상
  - DW(AF): `DW_DC.csv` — HeaderRow=5, Input=`DAC`, Output=`Position_0`, Up 62개(DAC 0→16383)/Down 61개(16320→0), Output 범위 0~16382로 정상
  - 처음엔 파일 앞부분만 보고 `Position_0`이 전부 0이라고 오판했으나, 전체 데이터 확인 결과 DAC가 임계값(약 10000) 이상으로 올라가야 Position_0이 반응하는 정상적인 데드존 구간이었음 — Input/Output 지정이 올바름을 확인
- 남은 작업: Anosys/Robocube DC 샘플 CSV가 확보되면 동일 패턴으로 Data Type 추가

### 2026-07-22

- 목적: SETTLING TIME 기능의 그동안 진행 이력 정리 요청 + `Anosys` Data Type 검증 요청 대응
- 이력 정리 결과(코드/타임스탬프/산출물 기반 소급 확인, 이 문서에는 반영 안 돼 있었음):
  - SETTLING TIME은 2026-07-16~07-20 사이 신규 구현됨: `Services/SettlingCsvReader.cs`, `Services/SettlingMetrics.cs`, `ViewModels/SettlingResultRow.cs`, `ViewModels/SettlingConditionTolerance.cs`, `ViewModels/SpecViewModels.cs`(SettlingSpecViewModel), `ViewModels/PlotFileItem.cs`, `Views/PlotView.xaml(.cs)`, `MainViewModel.cs`
  - 구현 당시 지원 Vendor: `DW`, `Robocube`, `K-star` (enum/리더/UI/판정/Export 모두 연동)
  - K-star: `SW DATA/AF/Settling/K-star`(L1~L3.xlsx) 실측 Summary 산출물 확인, 값도 9~10ms대로 합리적 → 검증됨
  - Robocube: `SW DATA/AF/Settling/Robocube` 실측 Summary 산출물은 있으나 Settling(ms) 값이 대부분 0 → 기본 Tolerance Band(100)가 실측 변위보다 커서 생기는 파라미터 튜닝 문제로 판단
  - DW: Reader/UI는 있으나 실측 Export 산출물이 어디에도 없어 미검증 상태로 확인
- 금일 변경: `Anosys` Data Type 신규 추가
  - `SW DATA/AF/Settling/Anosys`의 실제 CSV(`ANOSYS_SETTLING_MD21512B0_1/2/5/6.csv`, `settling.csv`)를 직접 열어 구조 확인 — 헤더 없는 순수 숫자 CSV, (Time,Value) 쌍이 4채널 반복(8열), 1행=1000개 시점(0~100ms, 0.1ms 간격)
  - `SettlingVendor`에 `Anosys` 추가, `SettlingCsvReader.ReadAnosys` 신규 구현(헤더 없는 N채널 Time/Value 쌍 파싱, explicitTarget 없음 → Robocube와 동일하게 Tail Average를 target으로 사용, JudgeStartTimeMs=0 — 기록 시작부터 이미 움직이는 구간이라 DW와 동일하게 보정 불필요)
  - `MainViewModel.cs`의 `SettlingVendorOptions`에 `"Anosys"` 옵션 추가
  - 수정 파일: `Services/SettlingCsvReader.cs`, `ViewModels/MainViewModel.cs`
- 검증: 앱과 별개로 실제 소스 파일을 참조하는 임시 콘솔 하네스(스크래치 폴더, 작업 후 삭제)를 만들어 5개 실측 CSV 전체를 `SettlingCsvReader.Read(..., SettlingVendor.Anosys)` + `SettlingMetrics.Compute/ComputeDisplacement`로 직접 실행
  - 모든 파일에서 4채널 × 1001행 정상 파싱 확인(에러 없음)
  - 채널별 head/tail 평균, 변위(displacement), overshoot 값이 물리적으로 타당한 범위로 계산됨을 확인
  - 다만 기본 Tolerance Band(100)로는 전 채널 SettlingTime=0ms로 나옴(변위가 5~53 단위 수준이라 100은 지나치게 큼) — Robocube와 동일한 유형의 파라미터 튜닝 필요. 변위의 5% 수준 band로 재계산하면 37~100ms 범위의 값이 나와 계산 로직 자체는 정상 동작함을 확인
  - `settling.csv`가 `ANOSYS_SETTLING_MD21512B0_1.csv`와 완전 동일 파일임을 확인(중복) — 폴더 Import 시 중복 집계 주의 필요
- 수정 파일 빌드: `dotnet build --no-restore` 결과: 오류 0개, 경고 2개(기존 SkiaSharp NU1701 + 임시 wpftmp 프로젝트 중복 경고, 신규 오류 없음)
- 사용자 확인에 따라 채널 1,2=Position(위치), 3,4=Current(전류)로 확정. Current는 K-star가 Current 컬럼을 무시하는 것과 동일한 방식으로 Settling 판정 대상에서 제외하기로 결정
- `ReadAnosys`를 Position 채널만 파싱하도록 수정(Current 채널 파싱 자체를 생략), 라벨을 `Ch1..N` → `Position1`/`Position2`로 변경
- 수정 파일: `Services/SettlingCsvReader.cs`
- 재검증: 앱 실행 중 `JahwaInspector.Light.exe`(PID 20692)가 산출물 파일을 잠그고 있어 빌드가 1차 실패 → 사용자 승인 받아 프로세스 종료 후 재빌드 성공(오류 0개)
- 임시 콘솔 하네스로 5개 CSV 재실행: 파일당 `Position1`/`Position2` 2개 시리즈만 반환됨(Current 제외 확인), Settling Time/Overshoot 값은 이전과 동일하게 정상 계산됨
- 남은 작업: 적정 Tolerance Band 기본값/조건별 값 산정(Position 채널의 실측 변위 기준), `settling.csv` 중복 정리, DW 실측 검증, Robocube 기본값 재검토

### 2026-07-13

- 기존 소스와 수정 시각을 바탕으로 프로젝트 맥락 복원
- 지속적인 인수인계를 위해 이 작업 현황 문서 생성
- FRA 필터의 `IC Vendor` 명칭과 구조를 SCAN과 같은 `Data Type` 방식으로 변경
- 선택한 FRA Data Type이 검색하는 헤더 후보를 화면에 표시
- 수정 파일: `Services/FraDataTypes.cs`, `Services/BodeCsvReader.cs`, `ViewModels/MainViewModel.cs`, `MainWindow.xaml`
- 이전 `IcVendor`/`IC Vendor` 참조가 남지 않은 것을 확인
- `dotnet build --no-restore` 결과: 오류 0개, 경고 2개(기존 SkiaSharp 패키지 호환성 NU1701)
- 사용자 확인에 따라 AKM 헤더를 `Frequency [Hz]`, `Gain [dB]`, `Phase [deg]`로 확정
- 사용자 확인에 따라 DW 헤더를 `Frequency`, `Magnitude`, `Phase`로 확정
- 수정 파일: `Services/FraDataTypes.cs`
- 헤더 확정 후 `dotnet build --no-restore` 재검증: 오류 0개, 기존 NU1701 경고 2개
- FRA 설정 영역을 компакт한 설정 카드와 CSV 헤더 안내 카드로 분리
- 헤더 후보를 Frequency/Gain/Phase 행으로 정렬해 표시
- 수정 파일: `MainWindow.xaml`, `ViewModels/MainViewModel.cs`
- FRA UI 정리 후 `dotnet build --no-restore` 검증: 오류 0개, 기존 NU1701 경고 2개
- FRA 카드의 설명 문구, 색상 배경, 상태 배지를 제거하고 단일 흰색 카드로 단순화
- 왼쪽 하단의 `ExportOutputLabel`, `RawMergedLabel` 표시를 제거(내부 저장 및 결과 처리는 유지)
- 수정 파일: `MainWindow.xaml`
- 미니멀 UI 변경 후 `dotnet build --no-restore` 검증: 오류 0개, 기존 NU1701 경고 2개
