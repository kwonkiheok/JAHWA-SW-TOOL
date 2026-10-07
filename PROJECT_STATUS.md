# MeasureHub 작업 현황

최종 갱신: 2026-10-07

## 프로젝트 개요

- .NET 8 WPF 기반 측정 데이터 분석 도구
- 지원 검사: FRA, DC SENSITIVITY, SCAN, SETTLING TIME, TILT, SINE WAVE, CIRCLE TEST, SQUARE TEST
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
- TILT 분석 신규 구현: `robocube` Data Type 1개 지원(2026-07-24)
  - SCAN과 같은 Robocube 파일의 `TiltTx`/`TiltTy` 컬럼을 읽어 `sqrt(Tx²+Ty²)`로 Tilt 크기를 직접 계산(파일 자체의 `TiltV` 컬럼은 사용 안 함)
  - Up/Down 구간 전체 중 최대치(Peak) 기반 판정, Spec Tilt(abs max) 1개 값과 비교
  - 그래프는 Target-vs-Output 라인이 아니라 Tx(X축)-Ty(Y축) 4분면 산점도, Up=Orange/Down=Blue, 원점 크로스헤어 + Spec 반경 점선 원 표시
  - TILT 요약 CSV/Excel/PNG/오류 로그를 SCAN과 동일한 구조로 `OUTPUT/TILT/` 아래 출력
- SINE WAVE 분석 신규 구현: `AKM`, `DW` Data Type 2개 지원, X/Y 축 모두 판정(2026-08-06, DW 추가 2026-08-07)
  - 파일 헤더에 이미 계산돼 있는 `Max Err`/`P2P Err`/`Cnt NG`/`Err Flg` 값은 신뢰하지 않고, `time[ms]/Posi_X/Hall_X/Diff_X`(+ Y) 원본 시계열에서 `|Diff|` 기준으로 MaxErr/P2P Err를 X/Y 각각 직접 재계산(TILT와 동일한 원칙)
  - 판정은 재계산한 MaxErrX/MaxErrY의 절대값과 사용자 입력 Spec MaxErr(abs max, X/Y 공통 1개 값) 비교
  - 그래프는 ScottPlot Multiplot으로 X축/Y축 패널을 위/아래로 분리, 각 패널 안에서 Posi(target)와 Hall(실제)을 같은 왼쪽 축 스케일로 겹쳐 그리고 Diff는 오른쪽 축(+±Spec 점선)에 표시(최초 구현은 X만 단일 패널이었으나 사용자 피드백으로 X/Y 분리 + Posi/Hall 오버랩 강화)

## 최근 확인된 작업

- 2026-10-07: FRA History 문서 생성 기능 추가 (1차 테스트, FRA 만, 브랜치 `feature/template-report`)
  - 배경: 팀장님 요청인 컨트롤 튜닝 문서(`JAHWA_Control_Tuning_문서 - 양식.xlsm`) 자동 작성을 검토했다.
    - Copilot Studio 는 문서중앙화(M:\)에 접근할 수 없고 그래프를 삽입할 수 없어서 쓸 수 없었다.
    - 그래서 MeasureHub 에서 직접 문서를 만들기로 했다.
    - 형식은 예시 파일(`workspace\양식예시\FRA_History_예시_MeasureHub.xlsx`)로 사용자와 확정했다.
  - 동작
    - 상단 바의 **[Control Tuning 작성]** 버튼으로 창을 연다. 모달리스라 창을 열어 둔 채 메인 창에서 폴더를 바꿔 Import/Export 할 수 있다.
    - 축 탭(OIS X / OIS Y / AF)에서 "현재 FRA 결과 가져오기"를 누르면 마지막 FRA Export 의 `FraResults` 와 `PlotFiles` 를 가져온다.
    - 시료를 선택하고 그룹 버튼(최대 4개, 이름 입력 가능)을 누른다. 파일명 자동 인식은 하지 않는다.
    - "문서에 추가"를 누르면 xlsx 의 지정 시트 맨 아래에 빌드 블록 1개가 추가된다. 파일이나 시트가 없으면 새로 만든다.
  - 블록 형식
    - 헤더(개발 단계 ~ 검토 의견)는 블록 전체에 세로 병합한다.
    - 축마다 결과표를 둔다: Group / No. / File / Judge / 10Hz Gain / GM / PM / -3dB PM, Min / Max / Avg / STDEV 수식. Spec 미달 값은 빨간색이다.
    - 오른쪽에 그룹별 Bode 오버레이를 둔다(`PlotImageExporter.SaveBodeOverlayPng`).
      - 그래프 칸 크기에 맞춘 비율로 2배 해상도로 그려서 빈 공간이 생기지 않는다.
    - 측정하지 않은 축은 "측정 없음" 1행으로 표시한다.
  - **빌드 목록(편집·삭제)** 탭
    - 문서의 빌드 블록을 B열(개발 단계) 세로 병합 범위로 찾아 목록으로 보여 준다. 별도 표시를 남기지 않으므로 이전에 만든 문서도 그대로 쓸 수 있다.
    - 헤더 6칸을 고친 뒤 [저장]하면 문서에 반영된다(`FraHistoryWriter.UpdateHeader`).
    - [삭제]하면 블록 행과 그 안의 그래프를 지우고 아래 블록을 위로 당긴다(`FraHistoryWriter.DeleteBuild`).
    - ClosedXML 0.104 제약: 불러온 그림을 지운 뒤 같은 저장에서 그림을 새로 넣으면 파일이 깨진다(재열기 시 ArgumentOutOfRangeException).
      - 그래서 삭제는 두 번에 나눠 저장한다. ① 남길 그림을 메모리에 기억하고 그림 전부와 해당 행을 지운 뒤 저장 → ② 다시 열어 남길 그림을 새 위치에 추가하고 저장.
      - 원본이 깨지지 않도록 임시 복사본에서 작업한 뒤 바꿔 넣는다.
    - `XLPicturePlacement.MoveAndSize` 를 주면 Excel 이 파일을 "복구"해야 열 수 있어서 쓰지 않는다. 따라서 Excel 에서 직접 행을 지우면 그래프 그림이 남는다. 블록 삭제는 앱에서 한다.
    - 시험: 새로 만들기 → 추가 2회 → 다른 시트 추가 → 헤더 편집 → 첫 블록 삭제 → 마지막 블록 삭제 → 다시 추가까지 진행했다. Excel 로 열어 그림 위치가 정상이고 복구 메시지가 없는 것을 확인했다.
    - UI 자동화로 버튼 → 창 열림 → 목록 탭 표시까지 확인했다.
  - 마지막 입력값(파일 경로, 시트, 제작처, 담당자, 그룹 이름)은 `%AppData%\MeasureHub\fra_history.json` 에 기억한다.
  - 추가 파일
    - `Services/FraHistory/FraHistoryModels.cs`
    - `Services/FraHistory/FraHistoryWriter.cs` (ClosedXML)
    - `ViewModels/FraHistoryViewModel.cs`
    - `Views/FraHistoryWindow.xaml(.cs)`
  - 수정 파일
    - `MainWindow.xaml`: 버튼 추가
    - `ViewModels/MainViewModel.cs`: `OpenFraHistoryCommand`, `GetFraHistorySamples()`, `GetFraHistorySpec()` 추가. 기존 FRA 계산과 Export 는 변경하지 않았다.
  - 검증
    - `dotnet build` 오류 0 (경고는 기존 NU1701 뿐)
    - BaselineCapture 를 master(3fce7dc)와 작업 후로 비교한 결과 diff 0줄
      - 참고: `tools/baseline_before.txt` 자체는 이전 커밋들(SCAN AKM 기준선 통일 등) 이후 갱신되지 않아 14줄이 다르다. 이번 작업과는 무관하다.
    - Writer 단독 시험(새 파일 → 같은 시트 2회 추가 → 다른 시트 추가)을 Excel 로 열어 정상 표시를 확인했다.
  - 남은 작업
    - 앱 UI 실사용 확인(사용자)
    - 기존 양식(.xlsm)에 넣을지 별도 xlsx 로 쓸지 최종 결정
    - DC / Settling 등 다른 검사로 확장
    - `tools/baseline_before.txt` 갱신 여부 결정

- 2026-09-28: DC SENSITIVITY 그래프 이미지 Y축도 0~5000(1000 간격)으로 고정(`DcFixedYTicks`, 단일/오버레이 모두)
  - 사용자 요청. 이로써 X/Y 모두 고정, 5000 초과 값은 클리핑됨

- 2026-09-28: DC SENSITIVITY 그래프 이미지 X축을 0~5000(1000 간격)으로 고정
  - 배경: X축이 파일별 데이터 min/max를 `BuildNiceTicks`로 올림해 정해져서, 최대 Input이 4000을 조금만 넘어도
    축 끝이 5000이 되는 등 파일마다 4000/5000으로 달라졌음
  - 수정 파일: `Services/Rendering/GdiPlotRenderer.cs` — `SaveDcPlotPng`(단일/선택 저장), `SaveDcOverlayPng`(오버레이)
    둘 다 `DcFixedXTicks`(0~5000) 사용. 범위 밖 점이 축 밖으로 그려지지 않도록 시리즈 그릴 때 `plotRect` 클리핑 추가
  - (최초에는 Y축 자동 유지, 같은 날 Y축도 고정으로 변경) 화면 표시(ScottPlot) 그래프는 변경 안 함
  - `dotnet build` 오류 0. **실제 PNG 출력 확인은 사용자 확인 필요**
  - 남은 작업: 필요 시 고정 범위를 설정(`DcSensitivitySpec`)에서 바꿀 수 있게 옵션화

- 2026-08-28: Export 확인창/실패 알림을 전 검사항목에서 동일하게 맞춤
  - 배경: 사용자가 "TILT Export 확인" 창을 보고 "이 창이 뜨는 항목과 안 뜨는 항목, 실패창이 뜨는 항목과 안 뜨는 항목을 구별해달라"고 요청.
    조사 결과 SCAN/TILT/SINE WAVE/CIRCLE/SQUARE 5개만 확인창+실패창+실패로그를 갖고 있었고
    FRA/DC SENSITIVITY/SETTLING TIME 3개는 셋 다 없었음(실패는 화면 하단 라벨에 10건까지 텍스트로만 표시)
  - 가장 심각했던 문제: **FRA는 파일 루프에 개별 try/catch가 아예 없었다.** 파일 100개 중 1개만 예외를 내도
    루프 전체가 중단되고 사용자에겐 라벨에 `Error: ...` 한 줄만 떴다. 다른 항목은 모두 파일별로 격리돼 있었음
  - 추가로 발견: `BodeCsvReader`는 헤더를 못 찾으면 예외 대신 1/2/3열 폴백을 쓰기 때문에, 형식이 다른 파일이
    예외 없이 0건으로 조용히 통과해 빈 결과 행이 생겼다. `rawPts.Count == 0`이면 실패로 잡도록 보강
  - 구현: 기존 5개 항목의 전용 메서드(`ConfirmScanExport` 등)는 그대로 두고(회귀 위험 회피),
    새로 붙이는 3개는 공용 헬퍼를 사용 — `EnsureFilesToProcess`(0건 가드), `ConfirmExportDialog`,
    `SaveExportFailureLog`, `ShowExportFailureMessage`, `ShowExportExceptionMessage`
  - 0건 가드는 기존 5개 항목에도 추가해 8개 전부 통일. 실패 로그 파일명은
    `FRA_ErrorLog.txt` / `DC_Sensitivity_ErrorLog.txt` / `SETTLING_ErrorLog.txt`
  - 검증: 8개 Export 경로를 중괄호 매칭으로 파싱해 6개 항목(0건가드/확인창/실패창/실패로그/예외팝업/파일별격리)이
    전부 O인지 확인. 정상 3개+깨진 2개를 섞은 FRA 폴더로 실제 리더를 돌려 "성공 3 / 실패 2"가 나오는 것까지 확인
    (예전 동작이었다면 깨진 파일 2개가 빈 결과로 조용히 섞여 들어갔음)
  - `dotnet build` 오류 0/경고 0. **실제 GUI에서 각 항목 확인창·실패창이 뜨는지는 사용자 확인 필요**


- 2026-08-28: 체크한 파일만 그래프 이미지로 저장하는 기능을 전 검사항목으로 확장
  - 배경: FRA에만 체크박스+"선택 그래프 추출"이 있었고 나머지 항목엔 없었음. SETTLING에는 "선택 파일 이미지 저장" 버튼이
    이미 있었지만 (1) 체크박스가 `IsFraInspection`으로 묶여 FRA에서만 보였고 (2) 그 버튼은 체크가 아니라 목록에서
    하이라이트된 파일 1개만 저장하는 동작이라 사실상 쓸 수 없는 상태였음
  - 사용자 확정: 파일당 개별 PNG 저장을 기본으로 하고, DC SENSITIVITY만 추가로 "전체를 합친 오버레이"도 제공.
    SETTLING 버튼도 체크박스 기준으로 통일
  - UI(`Views/PlotView.xaml`): 체크박스를 검사항목과 무관하게 항상 노출(단 ALL OVERLAY 가상 항목은 `InvBoolToVis`로 제외).
    "선택 파일 이미지 저장"(공통) + "선택 그래프 오버레이"(FRA) + "선택 파일 오버레이 저장"(DC) 3개 버튼으로 정리,
    라벨 입력란은 FRA/DC 양쪽에서 보이도록 DataTrigger로 변경
  - ViewModel: `SelectedPlotCount`/`CanSaveSelectedPlotImages`/`CanSaveSelectedDcOverlay` 추가(기존 `SelectedOverlayCount`는
    FRA 전용이라 `Points.Count > 0` 조건이 있어 다른 항목에 못 씀), `SaveSelectedPlotImagesCommand`/`SaveSelectedDcOverlayCommand` 신규.
    `SaveSelectedSettlingImagesCommand`는 동일한 `SaveSelectedPlotImages`를 호출하도록 변경
  - `SavePlotImagesForItem`이 항목 종류(`IsSettling`/`IsSquareTest`/`IsSinewave`/`IsCircleTest`/`IsTilt`/`IsScan`/`IsDcSensitivity`/FRA)에 따라
    기존 PNG 저장 함수를 그대로 재사용해 분기. 저장 위치는 Export 배치와 동일한 `OUTPUT/<검사명>/Plots/`
    (DC만 폴더명이 `DC sensitivity`라 `CurrentOutputFolderName()`에서 별도 매핑)
  - SCAN만 예외적으로 원본 파일을 다시 읽는다 — 그래프에 range별 지표 주석이 들어가는데 `PlotFileItem`에는 지표가 없어서,
    Export와 같은 그림이 나오도록 `dataType.Read` → `ResolveScanRanges` → `ComputeMetrics`를 재수행
  - `SaveDcOverlayPng` 신규(`MainViewModel`): 파일마다 색을 바꾸고 Up=실선/Down=점선으로 겹쳐 그린 뒤 파일명 범례 표시
  - 주의해서 처리한 점: `RelayCommand`가 `CommandManager.RequerySuggested`를 쓰지 않고 수동 `RaiseCanExecuteChanged`만 하므로,
    체크 변경·목록 갱신 시점에 새 명령들을 반드시 갱신해야 버튼이 활성화된다. 갱신 지점을 `RefreshSelectedPlotCommands()`로 통합
  - 검증: `dotnet build` 오류 0/경고 0(XAML 포함), 앱 기동 스모크 테스트 통과.
    **실제 GUI에서 체크 후 저장한 이미지 결과물은 미확인 — 사용자 확인 필요**

- 2026-08-28: 헤더 이름 변형 대응을 전 검사항목에 공통화 (`Services/HeaderMatch.cs` 신규)
  - 배경: "같은 설비사·같은 검사인데 장비 버전/설정에 따라 헤더 이름이 달라 값이 이상하게 나오거나 파일을 못 읽는다"는 문제. 실제 샘플에서 확인된 변형 —
    AKM SINE WAVE `Hall_X` ↔ `Hall_X[filter set]`, AKM SQUARE `time[ms],pos,read_vt,hall,read_mv`(5열) ↔ `time[ms],pos,hall`(3열),
    AKM CIRCLE은 SINE와 같은 역할 컬럼을 `Posi_X` 대신 `ReadVt_X`로 표기, Robocube SETTLING `Step 18um` ↔ `Step 180Code`
  - 조사 결과 같은 헬퍼가 중복 존재했음: `NormalizeName` 3벌, `FindBestMatchingColumn` 3벌, `DetectHeaderRow` 3벌, 숫자 파싱 9벌 이상.
    사본마다 규칙이 달라 Robocube 계열은 공백만 제거해 `LaserDis[um]` 같은 변형을 놓쳤고, 접두 일치 폴백은 AKM SINE/CIRCLE에만 있었음.
    Scan/DC의 `FindBestMatchingColumn`은 `ToDictionary`라 정규화 후 중복 헤더명이 있으면 예외로 죽는 잠재 버그도 있었음
  - `HeaderMatch` 매칭 규칙: (1) 정확 일치 → (2) 접두 일치 → (3) 부분 일치. 단 (2)(3)은 **걸리는 컬럼이 정확히 하나일 때만** 채택하고,
    둘 이상이면 채택하지 않고 다음 후보로 넘어간다. DW SINE WAVE 파일에 `X Target %`와 `X Target 12bit`가 함께 있어서
    단위 표기를 지우거나 모호한 부분 일치를 허용하면 엉뚱한 컬럼을 읽게 되기 때문 — 정규화에서 `%`·`12bit` 등 단위는 일부러 제거하지 않는다
  - 헤더 행 탐지는 "필요한 컬럼이 모두 있는 행"을 찾는 방식으로 바꿈(`HeaderMatch.FindHeaderRow`).
    앵커 1개만 보면 변형 시 행을 못 찾고, 반대로 느슨한 후보 1개만 보면 전문(preamble)의 `DateTime` 행이 `time`에 걸려 오검출됨
  - 적용 파일: `BodeCsvReader`(FRA), `ScanCsvReader`, `RobocubeScanReader`, `RobocubeTiltReader`, `DcSensitivityCsvReader`,
    `SettlingCsvReader`, `AkmSinewaveReader`, `DwSinewaveReader`, `CircleTestReader`, `AkmSquareReader`, `DwSquareReader`
  - 검증: 실제 데이터 전수 파싱 73건 전부 성공/0건 실패 (FRA 400개, SCAN·DC·SETTLING·SINE·CIRCLE·SQUARE·TILT 샘플 전체).
    값도 원본과 일치 확인(예: `result_SineWave_L1_Filter.csv` → PosiX0=1534/HallX0=1458/DiffX0=76, 3열 SQUARE → T0=2048/V0=2047).
    추가로 헤더를 일부러 변형한 파일 6종(`LaserDis [um]`, `TARGET / laser_dis`, `Target(12bit)`, `pos[code]/hall[code]`, `X Target 12bit(raw)`)을 만들어
    전부 정상 파싱되는 것까지 확인. 검증 하니스는 스크래치패드 `readtest/` 에 있음(프로젝트 밖)
  - 범위 제한: 조사에서 함께 발견된 DTO 의미 불일치(`DwSinewaveReader`가 Target/Position을 `PosiX`/`HallX` 필드에 담는 문제)와
    구분자 자동 감지 통일은 계산 계층까지 파급되므로 이번에 건드리지 않음 — 별도 작업으로 남김

- 2026-08-28: SETTLING 정착 시각 정의를 설비 측정값 기준에 맞춰 보정 (`Services/SettlingMetrics.cs`)
  - 배경: 설비 LOG(`SettlingTimeUp/Down`)와 앱 계산값을 비교하니 거의 모든 스텝에서 앱 값이 정확히 +1ms 정도 크게 나옴(예: 설비 10.000 ↔ 앱 11.000)
  - 원인: `settleIndex = lastOutside + 1` — Tolerance Band를 마지막으로 벗어난 시점의 **다음 샘플** 시각을 정착 시각으로 썼음.
    샘플 간격이 1ms라 구조적으로 항상 1샘플만큼 크게 나옴
  - 수정: "벗어난 그 시점"(`lastOutside`)의 시각을 사용하도록 변경. 트리거 이후 한 번도 벗어나지 않았으면 0ms
  - 주의: 설비 LOG(txt)는 앱이 읽지 않는다(파싱 코드 없음). 설비 자체 소프트웨어가 산출한 값이라 정의 차이를 맞춘 것
  - 미완료: 실제 데이터로 수정 후 값이 설비 LOG와 얼마나 일치하는지 재검증 필요

- 2026-08-12: SQUARE TEST Tolerance Band 기준을 "Target 절대값 %"에서 "이동량(displacement) %"로 변경
  - 배경: 사용자가 두 AKM 샘플로 직접 교차검증 요청 — `MD10840_square_45u_L_X2.csv`(헤더 `max settling time,16.4`)에 이동량 기준(±5%) 밴드를 적용해 재계산해보니 target=2048로 돌아오는 스텝들이 정확히 16.40ms로 나와 헤더값과 정확히 일치(전체 12스텝 중 최댓값은 18.0ms로 약간 더 큼). Target 절대값 기준이었을 때보다 훨씬 근접 — 이동량 기준이 실제 장비 계산 방식에 더 가깝다고 판단해 채택
  - 참고: Overshoot는 밴드 기준을 바꿔도 헤더의 "거의 0"과 여전히 크게 다름(재계산 시 300대 값) — OEM 장비가 Overshoot를 raw hall이 아닌 다른 방식(필터링된 값 등)으로 계산하는 것으로 추정되나, 원인은 미확인 상태로 남겨두고 우리 방식(직접 재계산) 그대로 유지하기로 사용자 확인
  - 수정 파일: `Services/SquareMetrics.cs`(`band = |Target - 스텝 시작 시점 값| × toleranceBandPercent%`로 변경 — 기존엔 `|Target| × %`였음), `Services/SquareTestDataTypes.cs`(MetricRuleDescription 문구를 "이동량 × %"로 갱신), `App.xaml`/`ViewModels/MainViewModel.cs`(레이블을 "Tolerance Band (% of Displacement)"로 변경)
  - 검증: X2 샘플에서 여러 스텝이 정확히 16.40ms로 헤더값과 일치 확인. `dotnet build` 오류 0건(exe 파일 잠금 경고만 존재)

- 2026-08-12: SQUARE TEST AKM Up/Down 통합 및 Tolerance Band 사용자 설정 기능으로 변경
  - 사용자 요청: (1) AKM은 Up/Down 그룹을 나누지 말고 전체 스텝 중 최댓값 1개(Settling/Overshoot 각 1개)만 내도록 변경(DW는 기존처럼 Up/Down 유지) (2) Tolerance Band를 파일 헤더에서 고정값으로 가져오던 것을 없애고, 화면에서 사용자가 "Target의 ±N%"로 직접 설정하게 변경
  - 답변: Settling Time은 스텝 트리거 시점(Target이 바뀌는 순간)을 t=0으로 잡고 그 이후 Tolerance Band를 벗어난 마지막 시점부터 다시 계속 안에 머무는 시점까지로 계산 — 트리거 시점의 값(=아직 안 움직인 이전 목표값)을 기준으로 이동 방향도 판단
  - 수정 파일: `Services/SquareTestModels.cs`(`SquareAxisSeries`에서 `ToleranceBand` 필드 제거, `SquareMetricsResult`에 통합용 `MaxSettlingMs`/`MaxOvershoot` 필드 추가), `Services/SquareMetrics.cs`(`Compute`가 `toleranceBandPercent`/`combineUpDown` 파라미터를 받아 스텝마다 `band=|Target|×pct/100`로 즉석 계산, `combineUpDown=true`면 Up/Down 구분 없이 전체 스텝 최댓값 1개만 산출), `Services/AkmSquareReader.cs`/`DwSquareReader.cs`(헤더에서 tolerance/amplitude 읽어 band 만들던 로직 제거), `Services/SquareTestDataTypes.cs`(`ComputeMetrics`에 `toleranceBandPercent` 매개변수 추가, AKM은 `combineUpDown: true`, DW는 `false`로 호출), `ViewModels/SquareResultRow.cs`+`SpecViewModels.cs`(통합용 `MaxSettlingMs`/`MaxOvershoot`, `SpecToleranceBandPercent`(기본 5%), `LastSettling`/`LastOvershoot` 추가), `ViewModels/MainViewModel.cs`(Export/Judge/요약 CSV·Excel/화면 표시 전부 통합 필드 반영), `App.xaml`/`MainWindow.xaml`(Tolerance Band % 입력 필드, Settling/Overshoot(Max) 컬럼 추가)
  - 검증: AKM 샘플(Y1)에 새 로직(Target ±5% 밴드, 전체 스텝 통합 최댓값) 적용 → Settling≈38.8ms/Overshoot≈1473 (기존 Up/Down 분리 시 Up 23.6ms·Down 25.6ms였던 것이 통합 후 더 큰 스텝의 값(38.8ms)으로 나옴 — band를 amplitude 기준에서 각 스텝 Target 기준으로 바꾸면서 348 근처 스텝의 band가 17.4로 더 좁아져 settling이 더 길게 잡힌 것도 확인). `dotnet build` 오류 0건(exe 파일 잠금 경고만 존재)

- 2026-08-12: SQUARE TEST 검사항목 신규 구현(AKM, DW 둘 다 우선 구현)
  - 배경: `SW DATA/OIS/Sqaure`(AKM/DW)의 샘플 분석 결과, 둘 다 "Target이 중심↔±amplitude를 반복하는 사각파" 구조로 동일 — AKM은 파일당 축 1개(`time[ms],pos,read_vt,hall,read_mv`), DW는 파일당 X/Y 축 2개(`,Target,X Position,Y Position`, `Settling Tolerance Max/Min` 등 preamble)가 한 파일에 들어있음(둘 다 SINE WAVE와 유사하게 시계열 반복 스텝 구조)
  - 사용자 확정 사항: pos/Target(목표) vs hall/Position(실측)으로 스텝을 자동 감지(값이 바뀌는 지점마다 분할, 트리거 이전 baseline 구간은 판정 제외), 스텝을 이동 방향(목표가 커지면 Up, 작아지면 Down)으로 분류해서 각각 최댓값(worst case)만 결과로 냄. 마지막 스텝이 파일 끝에서 잘려 어색한 값(0.2ms 등)이 나와도 최댓값 집계라 영향 없어 별도 예외처리 안 함. 헤더의 `max settling time`/`max overshoot`(AKM), `X/Y Settling Time`/`Overshoot`(DW)는 신뢰하지 않고 SETTLING TIME에서 이미 검증된 `SettlingMetrics.Compute`를 스텝마다 재사용해 직접 재계산(SINE WAVE/DW Target-Position과 동일 원칙) — DW 실측 재계산값이 헤더값과 근사(수 ms/수 code 단위)함을 교차 검증
  - Tolerance Band(설정된 목표에서 "정착했다"고 볼 범위)는 각 데이터 타입 헤더에서 그대로 가져옴: AKM은 `amplitude × tolerance(%amp)`, DW는 `(Settling Tolerance Max - Min)/2`. 판정 스펙(Pass/Fail 기준)은 별도로 화면에서 사용자가 입력(`Spec Max Settling Time (ms)`, `Spec Max Overshoot`) — Tolerance Band(측정 방법 파라미터)와 Spec(판정 기준)을 분리
  - 신규 파일: `Services/SquareTestModels.cs`(`SquareAxisSeries`/`SquareReadResult`/`SquareMetricsResult`), `Services/SquareMetrics.cs`(Target 전환 지점 자동 감지 → Up/Down 분류 → `SettlingMetrics.Compute` 스텝별 재사용 → 그룹별 최댓값), `Services/AkmSquareReader.cs`, `Services/DwSquareReader.cs`(SettlingCsvReader의 DW TextFieldParser 파싱 패턴 재사용), `Services/SquareTestDataTypes.cs`(`ISquareTestDataTypeHandler`/`SquareTestDataTypeRegistry`, AKM/DW 둘 다 등록), `ViewModels/SquareResultRow.cs`
  - 수정 파일: `ViewModels/SpecViewModels.cs`(`SquareTestSpecViewModel` 추가), `ViewModels/PlotFileItem.cs`(Square 전용 생성자 오버로드 — `IReadOnlyList<SquareAxisSeries>` 타입 자체가 이미 구분되므로 별도 wrapper record 불필요), `ViewModels/MainViewModel.cs`(SINE WAVE/CIRCLE TEST 블록을 본떠 `ExportSquareBatch`/`JudgeSquareBySpec`/`ApplySquareJudgementToAll`/`ConfirmSquareExport`/`BuildSquareMetadata`/`SaveSquareFailureLog`/`ShowSquareFailureMessage`/`BuildSquareSummaryTable` 추가, PNG는 기존 `SaveSinewaveAxisPlotPng`를 축 개수만큼 반복 호출해서 재사용, Excel 요약은 `SaveScanSummaryExcel` 재사용), `Views/PlotView.xaml.cs`(`RefreshSquarePlot` 추가 — 파일마다 축 개수가 다를 수 있어(AKM 1개/DW 2개) Grid Row를 동적으로 생성, 기존 `ConfigureSinewavePanel`을 그대로 재사용해 Target vs Value만 표시), `App.xaml`(SquareTestSpecViewModel DataTemplate), `MainWindow.xaml`(SquareResults DataGrid), `InspectionOptions`에 "SQUARE TEST" 추가
  - 검증: AKM 샘플(`MD10840_square_200u_M_Y1.csv`)과 DW 샘플(`L-1.csv`)을 Python으로 동일 알고리즘 재현해 사전 검증 — AKM은 Up 최대 Settling≈23.6ms/Overshoot≈1432(hall이 실제로 0까지 바닥을 찍는 실측 데이터라 오버슈트가 크게 나옴, 계산 버그 아님), Down 최대 Settling≈25.6ms/Overshoot≈1473. DW는 X/Y 모두 헤더의 Settling Time(4~6ms)/Overshoot(2~8 code)와 근사한 값 재현. `dotnet build` 오류 0건
  - 남은 작업: Anosys/Robocube는 Square 샘플 폴더가 비어 있어 미구현. Spec 기본값(Settling 30ms, Overshoot 170)은 임시값이라 실측 스펙에 맞게 조정 필요. 실제 GUI에서 그래프/PNG/Excel 출력물 최종 확인 필요(사용자가 "이후에 다시 수정작업" 예고)

- 2026-08-10: CIRCLE TEST Export PNG도 화면과 동일한 4분면 XY 원형 궤적으로 변경(기존엔 화면만 바꾸고 PNG는 시계열 그대로 남아있었음)
  - 수정 파일: `ViewModels/MainViewModel.cs`에 `SaveCirclePlotPng` 신규 추가(`SaveTiltPlotPng`와 동일한 GDI+ 렌더링 방식이나 원점 대신 데이터 중심 기준 축 범위, Up/Down 대신 Target/Hall 범례). `ExportCircleBatch`가 파일당 PNG 2장(X축/Y축 시계열) 대신 1장(XY 궤적)만 생성하도록 변경, `CircleResultRow.PlotPathY`는 더 이상 사용 안 함(빈 문자열)
  - 검증: `dotnet build` 오류 0건(exe 파일 잠금 경고만 존재)

- 2026-08-10: CIRCLE TEST 화면 그래프를 시계열 X/Y 2단 패널 → TILT와 동일한 원점 대칭 4분면 XY 산점도로 변경
  - 배경: CIRCLE TEST는 렌즈가 원을 그리며 움직이는 검사라, SINE WAVE처럼 시간축 시계열보다 ReadVt(목표 원) vs Hall(실측 궤적)을 XY 평면에 겹쳐 그리는 게 실제 궤적을 직관적으로 보여줌
  - 수정 파일: `Views/PlotView.xaml.cs`의 `RefreshCirclePlot`을 TILT의 `RefreshTiltPlot` 패턴(공유 `_wpfPlot` 1개, 원점 대칭 축 범위, 중심축 십자선)으로 교체 — 다만 TILT처럼 여러 파일을 겹쳐 그리진 않고 선택된 파일 1개의 Target/Hall만 표시
  - `RefreshPlot` 디스패치도 SINE WAVE(별도 WpfPlot 2개, PlotHost 직접 관리) 방식에서 벗어나 TILT/SCAN과 같은 공유 `_wpfPlot` 체인으로 이동

- 2026-08-10: CIRCLE TEST 검사항목 신규 구현(AKM 우선)
  - 배경: `SW DATA/OIS/circletest/AKM`에 `MD10840_CircleTest_6HZ_L1~L3.csv`, `MD10840_CircleTest_10HZ_L5.csv`, `MD10840_CircleTest_10HZ_M1.csv` 5개 샘플 확인. 헤더 구조가 SINE WAVE AKM과 거의 동일(`time[ms],ReadVt_X,Hall_X,Diff_X,ReadVt_Y,Hall_Y,Diff_Y`, `Threshold`/`Max Err`/`P2P Err`/`Cnt NG`/`Err Flg` 헤더 필드 포함) — Posi 대신 ReadVt라는 이름만 다름
  - 5개 파일 전부 Diff 컬럼을 직접 재계산해 헤더의 `Max Err X/Y`와 완전히 일치 확인(예: 6HZ_L1 → X:100, Y:190). SINE WAVE와 동일 원칙(헤더 신뢰 안 함, 직접 재계산) 적용, P2P는 SINE WAVE에서 이미 제거한 것과 동일하게 처음부터 미구현
  - 신규 파일: `Services/CircleTestReader.cs`(`CircleReadResult`, `time[ms]`/`ReadVt_X`/`Hall_X`/`Diff_X`(+Y) 파싱, AKM 필터 변형 대응용 StartsWith 폴백 포함), `Services/CircleTestMetrics.cs`(`CircleMetricsResult` — MaxErr만), `Services/CircleTestDataTypes.cs`(`ICircleTestDataTypeHandler`/`CircleTestDataTypeRegistry`, 현재 `AKM` 1개만 등록), `ViewModels/CircleResultRow.cs`
  - 수정 파일: `ViewModels/SpecViewModels.cs`(`CircleTestSpecViewModel` 추가, Spec 기본값 150 — AKM 샘플 헤더의 Threshold 150과 동일하게 설정), `ViewModels/PlotFileItem.cs`(Circle 전용 생성자 오버로드 추가 — Sinewave의 7-double 튜플 시그니처와 겹쳐 오버로드 모호성이 나므로 `CircleSample` record로 분리), `ViewModels/MainViewModel.cs`(SINE WAVE 블록을 그대로 본떠 `ExportCircleBatch`/`JudgeCircleBySpec`/`ApplyCircleJudgementToAll`/`ConfirmCircleExport`/`BuildCircleMetadata`/`SaveCircleFailureLog`/`ShowCircleFailureMessage`/`BuildCircleSummaryTable` 추가, PNG 저장은 기존 `SaveSinewaveAxisPlotPng` 재사용, Excel 요약은 기존 `SaveScanSummaryExcel` 재사용), `Views/PlotView.xaml.cs`(`RefreshCirclePlot` 추가 — SINE WAVE의 `ConfigureSinewavePanel`을 그대로 재사용해 X/Y 축마다 독립된 WpfPlot에 Target/Hall만 표시), `App.xaml`(CircleTestSpecViewModel DataTemplate), `MainWindow.xaml`(CircleResults DataGrid), `InspectionOptions`에 "CIRCLE TEST" 추가
  - 검증: `dotnet build` 오류 0건. 샘플 5개 전부 파싱 후 재계산한 MaxErrX/MaxErrY가 헤더값과 완전히 일치
  - 남은 작업: Anosys/DW/Robocube는 circletest 샘플 폴더가 비어 있어 미구현. 실제 GUI에서 그래프/PNG/Excel 출력물 최종 확인 필요

- 2026-08-10: SINE WAVE 화면 그래프에서 Diff/Spec 라인 제거, DW 신형 포맷(Target/Feedback/Position) 지원
  - 배경1(그래프): 화면 라이브 뷰(ScottPlot Multiplot)가 X축/Y축 두 패널을 WPF 안에서 제대로 분리하지 못해 두 패널 데이터가 뒤섞여 그려지는 버그 확인. Settling 검사에서 쓰던 방식(축마다 완전히 독립된 `WpfPlot` 컨트롤을 Grid에 쌓는 방식)으로 교체해 해결. 이후 사용자 요청으로 Diff(오차)/Spec 점선과 오른쪽 보조축도 화면에서 제거하고 Target/Hall만 표시하도록 단순화(`Views/PlotView.xaml.cs`의 `RefreshSinewavePlot`/`ConfigureSinewavePanel`)
  - 배경2(P2P 제거): 사용자 요청으로 P2P Err(X/Y) 지표를 결과 테이블/요약 CSV·Excel/화면 요약 패널에서 전부 제거. `Services/SinewaveMetrics.cs`가 이제 MaxErr만 반환(`SinewaveMetricsResult(double MaxErrX, double MaxErrY)`)
  - 배경3(DW 신형 포맷): `SW DATA/OIS/Sinewave/DW`의 샘플이 기존 `time[ms],Posi_X,Hall_X,Diff_X...` 포맷에서 완전히 다른 신형 포맷(`SINEWAVE_L_X_1~5.csv`, `SINEWAVE_L_Y_1~2.csv`)으로 교체됨. 새 포맷은 한 줄에 X/Y 값이 모두 있고, 값이 `%`와 `12bit` 두 단위로 중복 기록되며, 실측값이 `Target`(목표)/`Feedback`(원시 센서)/`Position`(필터 후 최종 출력) 3종으로 분리되어 있어 기존 `Posi/Hall` 2종 구조와 다름
    - 헤더에 `X/Y Delta 10bit Max`라는 필드가 있어 처음엔 이게 MaxErr 판정값인가 싶었으나, 7개 샘플 전부 대조한 결과 이 값은 실제로는 `SubDelta`(=Feedback-Position, 필터 전후 차이)와 정확히 일치하고 `Target vs Position`(추종 오차, ~100 코드대)과는 다른 값(~35~55 코드대)임을 확인. 사용자에게 확인 후, 이 검사의 목적(목표 추종 정확도)에 맞게 **Target 12bit - Position 12bit**를 Diff로 채택(헤더의 공식 판정값과는 다른 수치가 나옴을 인지하고 결정)
  - 신규 파일: `Services/DwSinewaveReader.cs` — `X/Y Target 12bit`, `X/Y Position 12bit` 컬럼만 읽어 Diff를 직접 계산(파일에 Diff 컬럼 자체가 없음), `SinewaveReadResult`의 Posi/Hall/Diff 필드에 매핑해 기존 `SinewaveMetrics`/화면 로직을 그대로 재사용
  - 수정 파일: `Services/SinewaveDataTypes.cs`의 `DwSinewaveDataTypeHandler.Read`가 `AkmSinewaveReader.Read` 대신 `DwSinewaveReader.Read`를 사용하도록 교체, 헤더/지표 설명 문구 갱신
  - 검증: 7개 샘플(`L_X_1~5`, `L_Y_1~2`) 전부 파싱 후 계산한 MaxErrX/MaxErrY가 각 파일의 축 스윕 방향과 합리적으로 일치(X 스윕 파일은 MaxErrX 102~108, MaxErrY 4 / Y 스윕 파일은 반대로 MaxErrX 4, MaxErrY 103~104). `dotnet build` 오류 0건
  - 남은 작업: 이전 DW 샘플(`L_1/L_2/M_1.csv`, `time[ms]` 포맷)은 샘플 폴더에서 이미 교체되어 사라짐 — 혹시 그 포맷의 실제 운용 데이터가 남아있다면 두 포맷을 함께 지원해야 할 수 있음(현재는 신형 포맷만 지원)

- 2026-08-07: SINE WAVE AKM 필터 변형 파일 지원(`Hall_X[filter set]`/`Hall_Y[filter set]` 헤더)
  - 배경: `SW DATA/OIS/Sinewave/AKM`의 `result_SineWave_L1_Filter.csv`, `result_SineWave_L1_filter_Set.csv`, `result_SineWave_L2Filter.csv` 3개 샘플은 `Posi_X/Diff_X` 등은 기존과 동일하나 `Hall_X`/`Hall_Y` 컬럼명 뒤에 `[filter set]` 접미사가 붙어 있어 기존 `FindColumn`의 완전 일치 방식으로는 컬럼을 못 찾아 로드 실패
  - 수정 파일: `Services/AkmSinewaveReader.cs`의 `FindColumn`에 완전 일치 실패 시 접두(StartsWith) 일치로 재탐색하는 폴백 추가 — `Hall_X[filter set]`이 `Hall_X`로 매칭됨. AKM/DW 모두 동일 Reader를 쓰므로 별도 Data Type 추가 없이 자동 지원됨
  - 검증: 3개 파일 모두 재계산한 MaxErr/P2P Err(X, Y)가 헤더의 `Max Err X/Y`, `P2P Err X/Y`와 완전히 일치(예: L1_Filter → X: 99/189, Y: 162/265) 확인

- 2026-08-07: SINE WAVE에 `DW` Data Type 추가
  - 배경: `SW DATA/OIS/Sinewave/DW`에 `L_1.csv`, `L_2.csv`, `M_1.csv` 3개 실측 샘플 신규 확보. 헤더 검사 결과 AKM 샘플과 컬럼 구조(`time[ms],Posi_X,Hall_X,Diff_X,Posi_Y,Hall_Y,Diff_Y`)가 완전히 동일 — 별도 파서 불필요
  - 수정 파일: `Services/SinewaveDataTypes.cs`에 `DwSinewaveDataTypeHandler` 추가(기존 `AkmSinewaveReader.Read`/`SinewaveMetrics.Compute`를 그대로 재사용), `SinewaveDataTypeRegistry.Handlers`에 등록. UI는 `SinewaveSpecViewModel.DataTypeOptions`가 레지스트리 `Names`를 그대로 바인딩하므로 화면 콤보박스에 자동으로 `DW`가 노출됨(추가 XAML 수정 불필요)
  - 검증: `dotnet build` 오류 0건. `L_1/L_2/M_1.csv` 3개 모두 직접 재계산한 MaxErr/P2P Err(X, Y 각각)가 파일 헤더의 `Max Err X/Y`, `P2P Err X/Y` 값과 완전히 일치(예: L_1 → X: 34/67, Y: 40/74)함을 교차 검증
  - 남은 작업: Anosys/Robocube는 Sine Wave 샘플 폴더가 비어 있어 여전히 미구현. 실제 GUI에서 DW 선택 시 그래프/PNG/Excel 출력물 최종 확인 필요(AKM과 동일 코드 경로라 위험은 낮음)

- 2026-08-06: SINE WAVE 검사항목 신규 구현(AKM 우선)
  - 배경: `SW DATA/OIS/Sinewave/AKM`의 `SINEWAVE_L_X_*.csv` 3개 샘플 확인 — `time[ms],Posi_X,Hall_X,Diff_X,Posi_Y,Hall_Y,Diff_Y` 구조, 헤더에 `Threshold`/`Permission`/`Max Err`/`P2P Err`/`Cnt NG`/`Err Flg`가 이미 계산돼 있으나 파일명이 `_X` 전용이라 Y는 정지값(1500 근방)만 존재
  - 사용자 확정 사항: 판정 기준은 파일 헤더 값을 믿지 않고 직접 재계산(TILT와 동일 원칙), 그래프는 Posi vs Hall 오버레이 + Diff 서브 표시, 지금은 X축만 구현하고 Y축 파일은 추후 샘플 확보 시 추가, Spec은 TILT처럼 화면에서 사용자가 직접 입력(Max Err 절대 한계 1개 값)
  - 신규 파일: `Services/AkmSinewaveReader.cs`(`SinewaveReadResult`, `time[ms]` 헤더 행 자동 탐색 후 Posi_X/Hall_X/Diff_X 파싱), `Services/SinewaveMetrics.cs`(`SinewaveMetricsResult` — `|Diff_X|` 최대값(MaxErr), `max-min`(P2P Err) 계산), `Services/SinewaveDataTypes.cs`(`ISinewaveDataTypeHandler`/`SinewaveDataTypeRegistry`, SCAN/TILT와 동일한 Data Type Registry 패턴 — 현재는 `AKM` 1개만 등록), `ViewModels/SinewaveResultRow.cs`
  - 수정 파일: `ViewModels/SpecViewModels.cs`(`SinewaveSpecViewModel` 추가), `ViewModels/PlotFileItem.cs`(Sinewave 전용 생성자 오버로드 추가 — 단순 `IReadOnlyList<double>` 4개 시그니처는 기존 DC Sensitivity 생성자와 파라미터 타입이 겹쳐 오버로드 모호성 오류(CS0111) 발생, TILT와 동일하게 `IReadOnlyList<(double Time, double Posi, double Hall, double Diff)>` 튜플 리스트로 시그니처 분리), `ViewModels/MainViewModel.cs`(TILT 블록 전체를 그대로 본떠 `ExportSinewaveBatch`/`JudgeSinewaveBySpec`/`ApplySinewaveJudgementToAll`/`ConfirmSinewaveExport`/`BuildSinewaveMetadata`/`SaveSinewaveFailureLog`/`ShowSinewaveFailureMessage`/`BuildSinewaveSummaryTable`/`SaveSinewavePlotPng` 추가, Excel 요약은 기존 `SaveScanSummaryExcel` 재사용), `Views/PlotView.xaml.cs`(`RefreshSinewavePlot` 추가 — FRA의 dual-axis 패턴처럼 왼쪽=Posi/Hall(Code), 오른쪽=Diff_X + ±Spec 점선), `App.xaml`(SinewaveSpecViewModel DataTemplate), `MainWindow.xaml`(SinewaveResults DataGrid). `InspectionOptions`/`ActiveSpec`/`SyncSelectionToPlot`/`SyncSelectionToTable`/`ResetResults`에는 이미 "SINE WAVE" 항목이 예약돼 있었음(플레이스홀더) — 이번에 실제 로직 연결
  - 검증: `dotnet build` 오류 0건(기존 SkiaSharp NU1701 경고 외 신규 경고 없음, 첫 시도에서 위 PlotFileItem 오버로드 모호성으로 1회 빌드 실패 후 시그니처 수정으로 해결). 임시 콘솔 하네스(스크래치 폴더, 작업 후 삭제)로 `SINEWAVE_L_X_1/2/3.csv` 3개 실측 파일을 `AkmSinewaveReader`+`SinewaveMetrics`로 직접 실행 — 3개 모두 1999개 샘플 정상 파싱되고, 직접 계산한 MaxErr/P2P가 파일 헤더의 `Max Err X`/`P2P Err X` 값과 완전히 일치(40/76, 42/79, 39/74)함을 교차 검증
  - **제약**: WPF GUI의 실제 시각적 렌더링(Posi/Hall 오버레이 모양, dual-axis Diff, Spec 점선)은 이 작업 환경에서 화면 캡처로 직접 확인이 불가능함 — 빌드와 데이터 레이어(Reader/Metrics) 검증까지만 수행했고, 실제 화면에서의 최종 확인은 사용자가 앱을 실행해서 진행 필요
  - 남은 작업: Y축 파일(`SINEWAVE_L_Y_*` 등) 샘플이 확보되면 동일 패턴으로 추가, Spec MaxErr 기본값(현재 "50"은 임시값)을 실측 스펙에 맞게 조정 필요, 실제 GUI에서 그래프/PNG/Excel 출력물 최종 확인 필요
- 2026-07-24: TILT 검사항목 신규 구현(Robocube 우선)
  - 배경: SCAN 버그 수정 중 확인된 Robocube 원본 파일(`AF/SCAN/Robocube/#1_*.csv`)에 `TiltTx`/`TiltTy`/`TiltV` 컬럼이 이미 포함돼 있음을 발견(`AF/Tilt/Robocube/` 아래 동일 파일명 폴더도 내용이 완전히 동일함을 확인) — 같은 원본 데이터에서 SCAN(Target vs LaserDis)과는 별도로 TILT(Tx vs Ty) 판정을 뽑아내는 구조
  - 사용자 확정 사항: 출력값은 Tx/Ty를 합친 크기이되 파일의 `TiltV` 컬럼을 쓰지 않고 직접 재계산, 판정은 Up/Down 전체 구간 중 최대치(Peak) 기준, 그래프는 Tx-Ty 평면 4분면에 Up/Down을 다른 색으로 표시
  - 리팩터링: `Services/RobocubeScanReader.cs`의 `FindSectionHeader`/`Split`/`FindColumn`/`TryParseDouble`/`ReadRangeHints`/`ReadSection`을 `internal`로 전환하고 `FindSectionHeader`가 임의의 필수 컬럼 목록(`params string[]`)을 받도록 일반화 — SCAN 동작이 리팩터링 전과 100% 동일함을 콘솔 하네스로 재검증 후 신규 `RobocubeTiltReader`가 동일 유틸을 재사용하도록 구현
  - 신규 파일: `Services/RobocubeTiltReader.cs`(`TiltReadResult`), `Services/RobocubeTiltMetrics.cs`(`TiltMetricsResult`, Peak 계산), `Services/TiltDataTypes.cs`(`ITiltDataTypeHandler`/`TiltDataTypeRegistry`, SCAN의 Data Type Registry 패턴과 동일 구조 — 현재는 `robocube` 1개만 등록), `ViewModels/TiltResultRow.cs`
  - 수정 파일: `ViewModels/SpecViewModels.cs`(`TiltSpecViewModel` 추가), `ViewModels/PlotFileItem.cs`(Tilt 전용 생성자 오버로드 추가 — DC Sensitivity 생성자와 파라미터 타입이 겹쳐 오버로드 모호성이 발생해 `IReadOnlyList<(double Tx, double Ty)>` 튜플 리스트 형태로 시그니처를 분리함), `ViewModels/MainViewModel.cs`(SCAN 블록 전체를 그대로 본떠 `ExportTiltBatch`/`JudgeTiltBySpec`/`ApplyTiltJudgementToAll`/`ConfirmTiltExport`/`BuildTiltMetadata`/`SaveTiltFailureLog`/`ShowTiltFailureMessage`/`BuildTiltSummaryTable`/`SaveTiltPlotPng` 추가, TILT Excel 요약은 기존 `SaveScanSummaryExcel`을 그대로 재사용), `Views/PlotView.xaml.cs`(`RefreshTiltPlot` 추가 — ScottPlot `Add.Circle`로 Spec 반경 점선 원, `Add.HorizontalLine(0)`/`VerticalLine(0)`으로 원점 크로스헤어), `App.xaml`(TiltSpecViewModel DataTemplate), `MainWindow.xaml`(TiltResults DataGrid)
  - 검증: `dotnet build` 오류 0건(기존 SkiaSharp NU1701 경고 외 신규 경고 없음). 임시 콘솔 하네스(스크래치 폴더, 작업 후 삭제 예정)로 `SW DATA/AF/Tilt/Robocube/#1_0deg_1/2.csv`, `#1_180deg_1/2.csv` 4개 실측 파일을 `RobocubeTiltReader`+`RobocubeTiltMetrics`로 직접 실행 — 4개 모두 Up/Down 31개씩 정상 파싱되고, 우리가 독자적으로 계산한 Peak 크기가 파일 자체의 `TiltV` 컬럼 최대값과 거의 일치함(예: 4.207 vs 4.21)을 교차 검증해 `sqrt(Tx²+Ty²)` 계산 로직의 정확성을 확인
  - **제약**: WPF GUI의 실제 시각적 렌더링(4분면 플롯 모양, 색상, Spec 원)은 이 작업 환경에서 화면 캡처로 직접 확인이 불가능함 — 빌드와 데이터 레이어(Reader/Metrics) 검증까지만 수행했고, 실제 화면에서의 최종 확인은 사용자가 앱을 실행해서 진행 필요
  - 남은 작업: 다른 Data Type(AKM/DW/K-star/Anosys)에 Tilt 컬럼이 있는 실측 샘플이 확보되면 동일 패턴으로 추가, Spec Tilt 기본값(현재 "3"은 임시값)을 실측 스펙에 맞게 조정 필요, 실제 GUI에서 4분면 플롯/PNG/Excel 출력물 최종 확인 필요
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

- SINE WAVE - AKM: X축만 구현됨, Y축(`SINEWAVE_L_Y_*` 등) 샘플 미확보. Spec MaxErr 기본값("50")은 임시값
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
- 재검증: 앱 실행 중 `MeasureHub.exe`(PID 20692)가 산출물 파일을 잠그고 있어 빌드가 1차 실패 → 사용자 승인 받아 프로세스 종료 후 재빌드 성공(오류 0개)
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
