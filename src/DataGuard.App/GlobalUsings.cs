// WinForms 상호운용(UseWindowsForms)을 켜면 System.Windows.Forms가 암시적 using에 포함되어
// Application·MessageBox 등이 WPF 타입과 충돌한다. 이 앱은 WPF 기반이므로 WPF 타입을 우선한다.
// WinForms 트레이 타입은 코드에서 WinForms. 별칭으로 정규화해 사용한다.
global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
