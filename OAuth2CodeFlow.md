# Chi Tiết Luồng Chạy Code (OAuth2 Authorization Code Flow)

Tài liệu này giải thích đường đi của dữ liệu (flow) từ khi bạn bấm vào trang bảo mật cho đến khi lấy được dữ liệu từ API. Ở đây tôi sẽ bóc tách chi tiết phân biệt **"Hàm bạn tự code"** và **"Hàm ẩn do Middleware tự động xử lý"**.

---

## Giai đoạn 1: Đòi hỏi quyền truy cập (Ủy quyền)

### Bước 1: Người dùng truy cập trang bảo mật
* Bạn mở trình duyệt, bấm vào menu **Secure Page**.
* Trình duyệt gọi lệnh `GET http://localhost:5003/Home/SecurePage`.
* Yêu cầu đi vào **WebClient**: Hàm `HomeController.SecurePage()` được trang bị mộc `[Authorize]`.

### Bước 2: Bị chặn và chuyển hướng ẩn (Do Middleware làm)
* ASP.NET Core nhận thấy bạn chưa đăng nhập (không có Cookie).
* Mộc `[Authorize]` kích hoạt `OpenIdConnectMiddleware` (do bạn config ở `WebClient/Program.cs`).
* **[HÀM ẨN]**: Middleware tự động xây dựng một đường link dài ngoằng và ra lệnh cho trình duyệt (mã 302 Redirect) chuyển hướng chớp nhoáng sang AuthServer. 
  * Đường link có dạng: `http://localhost:5001/connect/authorize?client_id=web-client&response_type=code&scope=openid profile api1 roles&redirect_uri=...`

---

## Giai đoạn 2: Xác thực & Cấp Mã Số (Authorization Code)

### Bước 3: AuthServer nhận yêu cầu cấp quyền
* Trình duyệt tự bay sang `http://localhost:5001/connect/authorize`.
* Chạm vào hàm **`AuthorizationController.Authorize()`** của **AuthServer**.
* Code kiểm tra: `await HttpContext.AuthenticateAsync(Cookie)` -> Phát hiện người dùng này cũng chưa đăng nhập vào máy chủ AuthServer.
* Code chạy lệnh `Challenge()`: Tiếp tục đá người dùng văng ra trang `/Account/Login`.

### Bước 4: Người dùng gõ Mật khẩu
* Bạn nhập "test/test" trên màn hình.
* Ấn Submit, dữ liệu gửi vào hàm **`AccountController.Login()`**.
* Code kiểm chứng đúng mật khẩu, ghi một cái Cookie chứng nhận **"Đã Đăng Nhập Tại AuthServer"**.
* Trình duyệt tự được chuyển hướng quay ngược lại `/connect/authorize`.

### Bước 5: AuthServer cấp mã Authorization Code
* Hàm **`AuthorizationController.Authorize()`** được gọi lần thứ 2. Nhưng lần này, Cookie đã có!
* AuthServer tạo ra một **"Hồ sơ điện tử"** (`ClaimsIdentity`), nhét tên tuổi của bạn vào trong đó.
* Các hàm `principal.SetScopes(...)` và `principal.SetResources("ResourceApi")` được gọi để "duyệt" quyền hạn và chốt Đích đến của thẻ.
* Cuối cùng, hàm `return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);` được chạy.
* **[HÀM ẨN] OpenIddict ngầm làm 2 việc:** 
  1. Tạo ra một đoạn chuỗi mã gọi là **Authorization Code** (ví dụ: `ABC123XYZ`) và lưu nó cất vào Database. 
  2. Bắt Trình duyện quay trở về `WebClient` với cái Code này: `http://localhost:5003/signin-oidc?code=ABC123XYZ`.

---

## Giai đoạn 3: Đổi Code Lấy Thẻ (Token Exchange)

Đây là bước cực kỳ bảo mật: cái Token thực sự không bao giờ được gửi qua Trình Duyệt. Nó được truyền thẳng qua "Cổng Hậu" (Back-channel) giữa hai Máy Chủ.

### Bước 6: WebClient bắt được Mã Code (Authorization Code)
* Trình duyệt chạy về `/signin-oidc?code=ABC...`. Bạn không hề thấy URL này vì nó nháy rất chớp nhoáng.
* **[HÀM ẨN]**: `OpenIdConnectMiddleware` của **WebClient** rình sẵn ở địa chỉ `/signin-oidc`. Nó tóm lấy cái `code`.

### Bước 7: Cuộc gọi dưới ngầm (WebClient ⇨ AuthServer)
* **[HÀM ẨN]**: Ngay dưới nền hệ thống, WebClient tự động mở 1 đường HTTP POST riêng tư chạy ngầm gọi sang `http://localhost:5001/connect/token`.
* Nó nộp cái vé `code` và Mật khẩu sảnh (`ClientSecret` do bạn gắn sẵn ở config).

### Bước 8: AuthServer Đổi Mã thành Token
* Yêu cầu dội thẳng vào hàm **`AuthorizationController.Exchange()`** của **AuthServer**.
* Hệ thống kiểm tra: "Vé Code này có đúng là của WebClient xin lúc nãy không? Secret đúng không?".
* Nếu tất cả hợp lệ, lệnh `SignIn(principal...)` được ban ra.
* **[HÀM ẨN] OpenIddict** tạo ra 2 thẻ là `id_token` (dành cho WebClient lưu tên) và `access_token` (dạng chuẩn thông dụng JWT vì ta đã set `.DisableAccessTokenEncryption()`). Trả kết quả JSON này thẳng cho WebClient (qua cuộc gọi ngầm).

### Bước 9: WebClient hoàn tất đăng nhập
* **[HÀM ẨN]**: WebClient nhận được `access_token` và `id_token`.
* Vì cấu hình `options.SaveTokens = true`, nó giấu nhẹm 2 cái thẻ này chôn sâu vào trong Cookie "Đăng nhập" của WebClient.
* Sau chót, Middleware ra lệnh đá trình duyệt nhảy sang điểm cuối cùng: `/Home/SecurePage` (Trang mà bạn muốn vào lúc đầu). Bạn hiện lên màn hình thành công!

---

## Giai đoạn 4: Đem Token đi xin dữ liệu 

### Bước 10: Nhấn nút gọi API (CallApi)
* Bạn nhấn nút "Call Protected Resource API".
* Yêu cầu nhảy vào hàm **`HomeController.CallApi()`** của WebClient.
* Code `GetTokenAsync("access_token")` lấy tấm thẻ Access Token nằm trong Cookie lúc nãy đào lên.
* Code mượn Ống Tiêm `HttpClient`, nhét thẻ vào bộ phận Header: `Authorization: Bearer <dãy_token>`.
* Bắn tiếp lệnh `GetAsync("/api/data")` bay sang cổng 5002.

### Bước 11: Máy chủ tài nguyên kiểm định (ResourceApi)
* Gói hàng tiếp cận biên giới cổng 5002.
* Rào chắn **`[Authorize]`** của `DataController` bật lên, kích hoạt bộ kiểm duyệt của **ResourceApi**.
* **[HÀM ẨN TỰ ĐỘNG]**: Bộ Validation của OpenIddict sẽ dò địa chỉ Lão Đại:
  1. Nó ngầm bắn 1 HTTP GET tải cấu hình tại `http://localhost:5001/.well-known/openid-configuration`.
  2. Nó tìm ra địa chỉ chứa chìa khóa công khai (JWKS) tại `/well-known/jwks` và tải bộ khóa này về.
  3. Nó đem chìa khóa ráp thử vào chữ ký điện tử trên thẻ `access_token` mà WebClient gửi tới. Thấy khớp chữ ký, khớp Issuer, khớp Audience (`ResourceApi`), và không bị hết hạn.
* Vượt qua trạm gác thành công!

### Bước 12: Đổ dữ liệu về (Hoàn tất)
* Hệ thống truyền gói hàng chạy sâu vào hàm **`DataController.Get()`** an toàn.
* Bạn dùng đối tượng `User` hiện tại để trích xuất ngay `Identity.Name` và `Claims` ra xài mà không cấn vấn đề gì.
* Return `Ok(...)` chuỗi JSON mã bí mật.
* `WebClient` nhận chuỗi JSON này và ném lên màn hình (View `SecurePage`). 

**KẾT THÚC HÀNH TRÌNH!** Kẻ xấu không thể lấy được JWT Access Token của bạn vì nó không bao giờ truyền qua thanh công cụ trình duyệt mà truyền ngầm giữa Server với Server. Kẻ xấu lấy trộm `Authorization Code` cũng chịu cứng vì họ không có `ClientSecret` để đi qua bước số 7.
