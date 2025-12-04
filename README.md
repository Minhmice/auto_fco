# FCZ Screen Automation Debugger

Ứng dụng Windows automation debugger cho game FC ONLINE (fczf.exe) với khả năng tự động tìm process, capture window, preview, và chạy automation theo rule-based scenarios.

## Tính năng chính

- **Tự động tìm process**: Tự động phát hiện và bám vào process `fczf.exe` khi game khởi động
- **Window Capture**: Capture và preview cửa sổ game real-time
- **Background Mode**: Ẩn cửa sổ game ra off-screen nhưng vẫn capture và auto được
- **Rule-based Automation**: Chạy automation theo scenarios được định nghĩa bằng JSON
- **Image Matching**: Sử dụng OpenCvSharp để match template images
- **Dev Mode**: Tools để debug và tạo scenarios

## Cấu trúc Project

```
FCZAutoDebugger/
├── FCZ.App/              # WPF Application (UI)
├── FCZ.Core/             # Core logic (capture, window, input, rule engine)
├── FCZ.Vision/           # OpenCvSharp helpers (template matching)
└── FCZ.Scenarios/        # Scenario JSON serializer
```

## Requirements

- .NET 8
- Windows 10/11
- Game FC ONLINE (fczf.exe) đang chạy

## Dependencies

- OpenCvSharp4 - Image matching
- System.Drawing.Common - Bitmap handling
- CommunityToolkit.Mvvm - MVVM helpers
- Microsoft.Windows.CsWin32 - P/Invoke helpers

## Cấu hình

App tự động tạo các folders trong `%AppData%\FCZAutoDebugger\`:
- `Scenarios/` - Chứa file JSON scenarios
- `Templates/` - Chứa template images (PNG)
- `logs/` - Log files (optional)

## Scenario Format

Scenarios được lưu dưới dạng JSON với các step types:
- `waitForImageThenClick` - Chờ image xuất hiện rồi click
- `typeText` - Gõ text
- `waitForImage` - Chờ image xuất hiện
- `clickTemplate` - Click vào template
- `clickPoint` - Click vào điểm cụ thể
- `wait` - Đợi một khoảng thời gian
- `conditionalBlock` - Rẽ nhánh logic
- `loop` - Lặp lại các steps
- `log` - Ghi log

## Build

```bash
dotnet build FCZAutoDebugger.sln
```

## Run

```bash
dotnet run --project FCZ.App/FCZ.App.csproj
```

## Background Mode (Ẩn cửa sổ Game)

Khi bật Background Mode, cửa sổ game sẽ được di chuyển ra ngoài viewport (off-screen) nhưng không bị minimize. Điều này cho phép:
- Windows vẫn render window content
- Capture vẫn hoạt động bình thường
- Automation vẫn có thể click và gõ phím
- User có thể làm việc với các ứng dụng khác
- Window không hiển thị trên taskbar nhưng vẫn hoạt động đầy đủ

**Lưu ý**: Background Mode và tính năng ẩn cửa sổ đã được gộp thành một. Khi bật, window sẽ được di chuyển off-screen. Khi tắt, window sẽ được đưa trở lại màn hình.

## Notes

- CaptureService hiện sử dụng `PrintWindow` API để capture window content
- Để sử dụng Windows.Graphics.Capture đầy đủ, cần implement Direct3D11 interop (có thể enhance sau)
- Tất cả scenarios và templates được lưu trong AppData, không hard-code trong project

