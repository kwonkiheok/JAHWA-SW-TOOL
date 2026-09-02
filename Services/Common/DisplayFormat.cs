namespace MeasureHub.Services;

/// <summary>
/// 화면·CSV·그래프에서 공통으로 쓰는 표시 형식.
/// ViewModel 과 렌더러가 같은 형식을 써야 하므로 한 곳에 둔다.
/// </summary>
public static class DisplayFormat
{
    /// <summary>측정 범위를 "min-max" 형태로 표시한다. 예: "500-3500"</summary>
    public static string Range(double min, double max) => $"{min:0.###}-{max:0.###}";
}
