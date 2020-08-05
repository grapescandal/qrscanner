using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QRRoi : MonoBehaviour
{
    public int yMin;
    public int yMax;
    public int xMin;
    public int xMax;
    public int w;
    public int h;

    public QRRoi(Rect targetRect, Rect rawRect, float webcamWidth, float webcamHeight)
    {
        float sx = targetRect.x - rawRect.x;
        float sy = targetRect.y - rawRect.y;
        float rw = webcamWidth / rawRect.width;
        float rh = webcamHeight / rawRect.height;
        this.xMin = (int)(sx * rw);
        this.yMin = (int)(sy * rh);
        this.w = (int)(targetRect.width * rw);
        this.h = (int)(targetRect.height * rh);
        this.xMax = this.xMin + this.w;
        this.yMax = this.yMin + this.h;
    }

    private float NotNegative(float value)
    {
        return value < 0 ? -value : value;
    }
}
