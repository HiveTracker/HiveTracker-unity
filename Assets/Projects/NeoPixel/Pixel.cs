using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Pixel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    Toggle thisToggle;

    void Start () {
        thisToggle = GetComponent<Toggle>();

        thisToggle.onValueChanged.AddListener(Changed);
    }

    public void OnPointerEnter(PointerEventData p)
    {
        if(UniNeoPixel.Instance.useHover)
        {
            Changed(true);
        }
    }

    public void OnPointerExit(PointerEventData p)
    {
        if (UniNeoPixel.Instance.useHover)
        {
            Changed(false);
        }
    }

    public void Changed(bool selected)
    {
        thisToggle.graphic.color = CUIColorPicker.Instance.Color;
        UniNeoPixel.Instance.SetPixelColor(int.Parse(this.transform.name), selected);
    }

}
