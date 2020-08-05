using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using ZXing;
using ZXing.QrCode;
using ZXing.Common;
using System.Threading;
using XSystem;
using BNKReminderServer.Models;

public class QRScanner : MonoBehaviour
{
    private bool cameraInitialized;
    private WebCamTexture webCamTexture;
    [SerializeField]
    private RawImage rawImage;
    [SerializeField]
    private AspectRatioFitter rawImageFitter;
    [SerializeField]
    private RectTransform target;
    [SerializeField]
    private AspectRatioFitter targetFitter;
    [SerializeField]
    private Text receivedText;
    private QRRoi qrROI;
    private Color32[] datas;
    private string decodingQRCodeResult = "";
    private bool decodingQRCode = false;
    [SerializeField]   
    private InputField qrCodeField;

    Rect defaultRect = new Rect(0f, 0f, 1f, 1f);
    Rect fixedRect = new Rect(0f, 1f, 1f, -1f);

    // Start is called before the first frame update
    void OnEnable()
    {
        if(RedeemManager.GetRedeemType() == 2)
        {
            CameraInit();
        }
        RedeemManager.SetDefaultValue();
    }

    public void SubmitQRCode()
    {
        Debug.Log(qrCodeField.text);
        StartCoroutine(QRFound(qrCodeField.text));
    }

    private void CameraInit()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Debug.Log("No camera detected");
            return;
        }

        for (int i = 0; i < devices.Length; i++)
        {
            if (!devices[i].isFrontFacing)
            {
                webCamTexture = new WebCamTexture(devices[i].name, Screen.width, Screen.height);
                break;
            }
        }

        if (webCamTexture == null)
        {
            Debug.Log("Unable to find back camera");
            return;
        }

        webCamTexture.Play();

        rawImage.texture = webCamTexture;

        cameraInitialized = true;
        decodingQRCode = false;
        decodingQRCodeResult = string.Empty;
        rawImage.gameObject.SetActive(true);

        if (cameraInitialized)
        {
            StartCoroutine(FindingQRCode());
            StartCoroutine(WaitUntilFound());
        }
    }

    // Update is called once per frame
    IEnumerator FindingQRCode()
    {
        float ratioRawImage = (float)webCamTexture.width / (float)webCamTexture.height;
        rawImageFitter.aspectRatio = ratioRawImage;
        Debug.Log(ratioRawImage);

        float ratioTarget = (float)target.rect.width / (float)target.rect.height;
        targetFitter.aspectRatio = ratioTarget;

#if UNITY_ANDROID
        rawImage.uvRect = webCamTexture.videoVerticallyMirrored ? fixedRect : defaultRect;
#elif UNITY_IOS
        rawImage.uvRect = !webCamTexture.videoVerticallyMirrored ? fixedRect : defaultRect;
#endif

        int orient = -webCamTexture.videoRotationAngle;
        rawImage.rectTransform.localEulerAngles = new Vector3(0, 0, orient);

        IBarcodeReader barcodeReader = new BarcodeReader();

        while (!decodingQRCode)
        {
            qrROI = new QRRoi(target.rect, rawImage.rectTransform.rect, webCamTexture.width, webCamTexture.height);

            List<Color32> roi = new List<Color32>();
            Color32[] pixels = webCamTexture.GetPixels32();

            for (int y = qrROI.yMin; y < qrROI.yMax; y++)
            {
                for (int x = qrROI.xMin; x < qrROI.xMax; x++)
                {
                    roi.Add(pixels[(y * webCamTexture.width) + x]);
                }
            }

            datas = roi.ToArray();

            yield return new WaitForEndOfFrame();

            if (decodingQRCode)
            {
                continue;
            }

            Thread t = new Thread(new ParameterizedThreadStart((arg) =>
            {
                var result = barcodeReader.Decode(datas,
                qrROI.w, qrROI.h);
                if (result == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(result.Text))
                {
                    return;
                }
                decodingQRCodeResult = result.Text;
            }));
            t.Start();
        }
    }
    private IEnumerator WaitUntilFound()
    {
        while (string.IsNullOrEmpty(decodingQRCodeResult))
        {
            yield return null;
        }

        Debug.Log($"decoded: {decodingQRCodeResult}");
        yield return QRFound(decodingQRCodeResult);
    }

    private IEnumerator QRFound(string result)
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            rawImage.gameObject.SetActive(false);
        }
        decodingQRCode = true;

        IWSResponse resp = null;
        yield return RedeemAPI.Redeem(XCoreManager.instance.mXCoreInstance,
            result,
            (r) => resp = r);

        PageController.ins.ShowPage(1);

        //TODO: in case of error, show message (resp.ErrorsString()) to user 
        if (!resp.Success())
        {
            Debug.Log($"redeem failed with message: {resp.APIError().errorID}");

            if (resp.APIError().errorID == 101)
            {
                receivedText.text = "ไม่มีโค้ดนี้อยู่";
            }
            else if (resp.APIError().errorID == 102)
            {
                receivedText.text = "โค้ดนี้มีผู้ใช้สิทธ์เต็มจำนวนแล้ว";
            }
            else if (resp.APIError().errorID == 103)
            {
                receivedText.text = "คุณได้ใช้โค้ดนี้ไปแล้ว";
            }

            yield break;
        }

        var redeemCodeResp = resp as RedeemAPI.RedeemCodeResponse;
        switch (redeemCodeResp.itemType)
        {
            case RedeemItemType.Star:
                var star = (int)redeemCodeResp.fulfillment["star"].AsDouble;
                receivedText.text = $"ไดัรับ STAR {star} ดวง";
                //PageController.ins.ShowPage(1); //ถ้าพี่บิ๊กจะย้ายหน้าให้ใช้ตรงนี้ครับ
                break;
            default:
                //TODO: show error message to user - redeem code success, but client cannot show the result
                Debug.LogError("redeem code success, but client cannot show the result");
                break;
        }

        IWSResponse resultP = null;

        yield return BNKUserProfile.Get(XCoreManager.instance.mXCoreInstance, (r) => resultP = r);

        BNKUserProfileResult userProfileResult = resultP as BNKUserProfileResult;
        BNKUserProfile profile = userProfileResult.profile;

        GameObject.Find("ProfileHolder").GetComponent<ProfileHolder>().SetProfile(profile);
    }
}
