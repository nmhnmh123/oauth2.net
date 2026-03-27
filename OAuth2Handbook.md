# Sổ tay OAuth2 - Học từ con số 0 (.NET 9)

## 1. Giới thiệu tổng quan mạng lưới

Chào mừng bạn! Dự án này được thiết kế để giúp bạn hiểu tường tận luồng hoạt động của **OAuth2** (một giao thức ủy quyền phổ biến nhất hiện nay). Để bạn đọc để dàng tưởng tượng cách thực tế các ứng dụng giao tiếp với nhau ra sao, project đã bỏ hoàn toàn Swagger (thứ thường che giấu đi các bước điều hướng - redirect) và chia thành 3 thành phần chạy độc lập trên 3 cổng (port) khác nhau:

1. **AuthServer (Cổng 5001)**: Đóng vai trò là **Authorization Server** (Máy chủ cấp quyền). Nơi tập trung lưu trữ tài khoản người dùng, xử lý việc Đăng Nhập (Login) và sau đó cấp phát các "Thẻ ra vào" (Token) cho các ứng dụng Client xin phép. Hệ thống cài đặt bằng thư viện OpenIddict.
2. **ResourceApi (Cổng 5002)**: Đóng vai trò là **Resource Server** (Máy chủ tài nguyên). Chứa các dữ liệu cần bảo vệ (ví dụ: thông tin cá nhân, số dư ngân hàng). Nơi này KHÔNG chứa trang đăng nhập, nó chỉ kiểm tra xem ai đến kêu cửa có mang theo "Thẻ ra vào" (Access Token) hợp lệ hay không.
3. **WebClient (Cổng 5003)**: Đóng vai trò là **Client application** (Ứng dụng phía người dùng). Ví dụ đây là trang web của đối tác báo chí muốn xin phép bạn để đọc "Thông tin cá nhân" từ ResourceApi. WebClient không giữ mật khẩu của bạn, nó sẽ dẫn (redirect) bạn tới AuthServer để bạn tự chứng minh bản thân.

---

## 2. Lý thuyết: Luồng Authorization Code Flow (có PKCE)

Dự án này sử dụng luồng **Authorization Code Flow**, an toàn nhất cho các ứng dụng Web hiện nay. Khi bạn xài WebClient, sự việc diễn ra theo trình tự sau:

1. **Khởi xướng**: Bạn bấm "Đăng nhập" trên màn hình của **WebClient** (Cổng 5003).
2. **Xin phép (Authorize)**: **WebClient** chuyển hướng (redirect) bạn sang trang `/connect/authorize` của **AuthServer** (Cổng 5001). Khi đi, nó còn gửi kèm các thông tin như: "Tôi là WebClient", "Tôi muốn xin quyền (scope) xem profile", và "Sau khi xử lý xong hãy trả kết quả về đường link redirect_uri này".
3. **Xác thực (Authenticate)**: **AuthServer** nhận thấy bạn chưa đăng nhập, nên nó hiện màn hình bắt bạn nhập Username/Password (`/Account/Login`).
4. **Cấp mã (Authorization Code)**: Bạn nhập đúng Username/Password. **AuthServer** xác nhận bạn chủ nhân thực sự, tự động chuyển hướng bạn quay ngược lại **WebClient** và kèm theo một đoạn mã định danh gọi là **Authorization Code** (Không phải token nhé!).
5. **Đổi Token**: Phía dưới nền (backend) của **WebClient**, WebClient cầm cái "Authorization Code" đó, bí mật gọi API (`/connect/token`) qua lại **AuthServer** để chính thức đổi lấy **Access Token** (Thẻ ra vào) và **ID Token** (Thẻ căn cước).
6. **Sử dụng Token**: Bây giờ trang **WebClient** đã cầm Access Token của bạn, nó được phép gọi tới **ResourceApi** (Cổng 5002) và bỏ token này vào Header của HTTP (`Authorization: Bearer <token_gì_đó_dài_ngoằng>`).
7. **Kiểm duyệt**: **ResourceApi** nhận yêu cầu, lấy cái Token trong Header ra, hỏi trung ương **AuthServer** xem token này có phải đồ thật và chưa hết hạn không. Nếu hợp lệ, trả về dữ liệu mật cho WebClient!

---

## 3. Cách đọc Code để hiểu hệ thống

Để thấm nhuần OAuth2, hãy đọc code theo đúng trình tự vòng đời của nó:

### Bước 1: Xem cách AuthServer được thiết lập (Cổng 5001)

- Xem `Program.cs` của **AuthServer**: Ở đây bạn sẽ thấy cách đăng ký OpenIddict làm Server. Cấu hình để nó cung cấp các URL cấp quyền (`/connect/authorize`, `/connect/token`).
- Xem `TestDataHostedService.cs`: Đây là nơi máy chủ tạo sẵn dữ liệu của các phần mềm đối tác (Ở đây ta khai báo ClientId là `web-client`, có quyền lấy Token, và URL trả về của nó là gì).
- Xem `Controllers/AccountController.cs`: Controller đơn giản để xử lý việc bạn gõ Username/Password ở trang Web.
- Xem `Controllers/AuthorizationController.cs`: TRÁI TIM CỦA OAUTH2. Chứa 2 hàm. Hàm `Authorize` là nơi nhận yêu cầu mở khóa từ web client và cấp đoạn mã Authorization Code. Hàm `Exchange` là nơi đổi Code lấy Access Token thực sự.

### Bước 2: Xem cách WebClient xin quyền (Cổng 5003)

- Xem `Program.cs` của **WebClient**: Kéo xuống đoạn `AddOpenIdConnect`. Đây là thư viện xịn xò của Microsoft, tự lo toàn bộ cái quá trình ở bước 2 -> 5 (trong phần lý thuyết ở trên). Bạn chỉ cần cấu hình ClientId (`web-client`), Secret, và địa chỉ của AuthServer.
- Xem `Controllers/HomeController.cs > CallApi()`: Tại đây, bạn sẽ thấy cách WebClient đào cái Access Token (Thẻ ra vào) được lưu tạm ở Cookie lên, nhét vào đối tượng `HttpClient` để gọi tới **ResourceApi** lấy số liệu bảo mật.

### Bước 3: Xem cách ResourceApi bảo vệ tài sản (Cổng 5002)

- Xem `Program.cs` của **ResourceApi**: Tại đây cấu hình OpenIddict làm hệ thống Kiểm duyệt (Validation). Khai báo địa chỉ của AuthServer để Resource API còn biết đường hỏi AuthServer khi có Token lạ gửi tới.
- Xem `Controllers/DataController.cs`: Cực kỳ đơn giản, chỉ cần gắn mộc `[Authorize]`. Cứ gọi đến là bắt buộc phải có Token ở tiêu đề mạng (Header).

---

## 4. Hướng dẫn chạy từng bước trải nghiệm

Phải tự tay bấm, nhìn trình duyệt nháy URL bạn code mới thấm được. Hãy tự chạy 3 tab Terminal:

**Trên Terminal 1 (Chạy AuthServer)**:

```bash
dotnet run --project AuthServer/AuthServer.csproj
```

**Trên Terminal 2 (Chạy ResourceApi)**:

```bash
dotnet run --project ResourceApi/ResourceApi.csproj
```

**Trên Terminal 3 (Chạy WebClient)**:

```bash

```

**Bây giờ hãy thử nghiệm**:

1. Mở trình duyệt web của bạn, truy cập vào **WebClient**: `http://localhost:5003`
2. Nhấn vào nút / Menu **Secure Page (Triggers Auth)**.
3. Chú ý lên thanh địa chỉ (URL bar), bạn sẽ thấy WebClient chớp 1 phát đẩy bạn sang AuthServer (`localhost:5001/Account/Login?ReturnUrl...`). Đây là bước chuyển hướng!
4. Màn hình yêu cầu Username, Password hiện ra. Nhập đại 1 cái gì đó (ví dụ: test / test) rồi bấm Login.
5. AuthServer xác nhận xong, lại chuyển hướng (Redirect) bạn trả về lại cho thằng **WebClient** (`localhost:5003/signin-oidc`).
6. Tại bước này, Microsoft Middleware đã tự động chộp lấy Authorization Code và đổi lấy Token. Bạn sẽ thấy mình đã vào vòng trong an toàn của trang web, và hiện lên các Token Claim!
7. Cuối cùng bấm **Call Protected Resource API**. Nó sẽ cầm Token vừa nhận đi sang cổng 5002 xin dữ liệu. Dữ liệu JSON bí mật sẽ từ cổng 5002 đổ về. Chúc mừng bạn đã hiểu OAuth2!
