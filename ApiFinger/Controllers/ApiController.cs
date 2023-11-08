using Microsoft.AspNetCore.Mvc;
using libzkfpcsharp;
using System.Drawing;
using System.Drawing.Imaging;

namespace ApiFinger.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ApiController : Controller
    {

        private readonly ILogger<ApiController> _logger;


        IntPtr mDevHandle = IntPtr.Zero;
        bool bIsTimeToDie = false;
        byte[] FPBuffer;

        byte[] CapTmp = new byte[2048];
        int cbCapTmp = 2048;

        private int mfpWidth = 0;
        private int mfpHeight = 0;

        const int MESSAGE_CAPTURED_OK = 0x0400 + 6;

        public ApiController(ILogger<ApiController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetApi")]
        public ActionResult Index()
        {
            zkfp2.Terminate();
            int ret = zkfperrdef.ZKFP_ERR_OK;
            if ((ret = zkfp2.Init()) == zkfperrdef.ZKFP_ERR_OK)
            {
                int nCount = zkfp2.GetDeviceCount();
                if (IntPtr.Zero == (mDevHandle = zkfp2.OpenDevice(0)))
                {
                    return Content("OpenDevice fail");
                }

                int size = 4;
                byte[] paramValue = new byte[size];
                zkfp.Int2ByteArray(1, paramValue);
                zkfp2.SetParameters(mDevHandle, 102, paramValue, size);

                zkfp2.GetParameters(mDevHandle, 1, paramValue, ref size);
                zkfp2.ByteArray2Int(paramValue, ref mfpWidth);

                size = 4;
                zkfp2.GetParameters(mDevHandle, 2, paramValue, ref size);
                zkfp2.ByteArray2Int(paramValue, ref mfpHeight);

                FPBuffer = new byte[mfpWidth * mfpHeight];

                while (!bIsTimeToDie)
                {
                    cbCapTmp = 2048;
                    int ret2 = zkfp2.AcquireFingerprint(mDevHandle, FPBuffer, CapTmp, ref cbCapTmp);
                    if (ret2 == zkfp.ZKFP_ERR_OK)
                    {
                        bIsTimeToDie = !bIsTimeToDie;

                        int width = mfpWidth;
                        int height = mfpHeight;

                        Bitmap image = CreateImageFromBuffer(FPBuffer, width, height);
                        Bitmap grayscaleImage = ConvertToGrayscale(image);
                        string base64String = ConvertImageToBase64(grayscaleImage);

                        zkfp.Int2ByteArray(1, paramValue);
                        zkfp2.SetParameters(mDevHandle, 103, paramValue, size);

                        Thread.Sleep(1000);

                        return Ok(base64String);
                    }
                }

                return BadRequest("Operation fail");
            }
            else
            {
                zkfp2.Terminate();
                return BadRequest("Initialize fail");
            }

        }

        private string ConvertImageToBase64(Bitmap image)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Png); // You can choose a different image format if needed.
                byte[] imageBytes = ms.ToArray();
                return Convert.ToBase64String(imageBytes);
            }
        }

        private Bitmap CreateImageFromBuffer(byte[] buffer, int width, int height)
        {

            Bitmap image = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            BitmapData bmpData = image.LockBits(new Rectangle(0, 0, 150, 200), ImageLockMode.WriteOnly, image.PixelFormat);

            IntPtr ptr = bmpData.Scan0;
            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, ptr, buffer.Length);

            image.UnlockBits(bmpData);

            return image;
        }

        private Bitmap ConvertToGrayscale(Bitmap original)
        {
            int width = original.Width;
            int height = original.Height;

            Bitmap grayscale = new Bitmap(width, height);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Color originalColor = original.GetPixel(x, y);
                    int grayValue = (int)(0.299 * originalColor.R + 0.587 * originalColor.G + 0.114 * originalColor.B);
                    Color grayColor = Color.FromArgb(grayValue, grayValue, grayValue);
                    grayscale.SetPixel(x, y, grayColor);
                }
            }

            return grayscale;
        }



        private void DoCapture()
        {
            byte[] paramValue = new byte[4];
            int size = 4;
            zkfp2.GetParameters(mDevHandle, 1, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpWidth);

            size = 4;
            zkfp2.GetParameters(mDevHandle, 2, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpHeight);

            FPBuffer = new byte[mfpWidth * mfpHeight];

            while (!bIsTimeToDie)
            {
                cbCapTmp = 2048;
                int ret = zkfp2.AcquireFingerprint(mDevHandle, FPBuffer, CapTmp, ref cbCapTmp);
                if (ret == zkfp.ZKFP_ERR_OK)
                {
                    bIsTimeToDie = !bIsTimeToDie;
                }
            }
        }
    }
}

