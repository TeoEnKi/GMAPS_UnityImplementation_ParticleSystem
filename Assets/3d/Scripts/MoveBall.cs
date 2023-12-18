using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class MoveBall : MonoBehaviour
{
    public bool enableBall;

    private void Start()
    {
        enableBall = true;
    }
    public void OnClick()
    {
        enableBall = !enableBall;

        TMP_Text moveBallBtn = EventSystem.current.currentSelectedGameObject.GetComponentInChildren<TMP_Text>();
        if (enableBall)
        {
            moveBallBtn.text = "Disable Ball";
        }
        else
        {
            moveBallBtn.text = "Enable Ball";
        }
    }
}
