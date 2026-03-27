using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ResourceApi.Controllers
{
    // Quy định Route gọi từ xa tới api hiện tại. Ví dụ: http://localhost:5002/api/Data
    [Route("api/[controller]")]
    [ApiController]
    
    // Cái Mộc CẤM CỬA CỰC KỲ QUAN TRỌNG:
    // Bạn gắn vào class này, thì bất cứ đứa nào chạy vào đây mà không xuất trình Thẻ Nhựa (Access Token Header: Bearer xxx)
    // Sẽ lập tức bị đá văng với Mã lỗi 401 Unauthorized (Chưa Xác Thực) hoặc 403 Forbidden (Cấm)
    [Authorize(Roles = "Admin")]
    public class DataController : ControllerBase
    {
        // Khi client nhắm vào hàm /api/Data bằng lệnh HttpGet
        [HttpGet]
        public IActionResult Get()
        {
            // Trả về Dữ liệu Mật cho họ.
            return Ok(new
            {
                Message = "Hello bạn! Đây là dữ liệu Tối Mật nằm trong két sắt của Resource API. Bạn Đã Đưa Chìa Access Token Nhập Của Cổng 5001 Và Tôi Đã Cho Bạn Vào!",
                
                // Ai là người sở hữu Token này? Tôi đọc được từ Claim Subject (Name)
                User = User.Identity?.Name,
                
                // Trong cái Access Token họ đưa có cái gì tôi lôi hết ra in lên màn hình
                Claims = User.Claims.Select(c => new { c.Type, c.Value })
            });
        }
    }
}
