using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExitManager : MonoBehaviour
{
    [SerializeField] float lastPressTime = -10f;
    [SerializeField] float interval = 1.0f;
    [SerializeField] UDPReceiver udpReceiver;
    [SerializeField] CSVWriter csvWriter;


    void Update(){
        if (Input.GetKeyDown(KeyCode.Escape)){
            if (Time.realtimeSinceStartup - lastPressTime < interval){
                QuitApp();
            }else{
                lastPressTime = Time.realtimeSinceStartup;
                Debug.Log($"もう一度押すと終了（{interval}秒以内）");
            }
        }
    }


    async void QuitApp(){
        await udpReceiver.StopServerAsync();
        csvWriter?.ForceClose();
        Application.Quit();
    }

}