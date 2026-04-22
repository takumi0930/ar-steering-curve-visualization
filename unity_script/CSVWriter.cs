using System;
using System.IO;
using System.Collections;
using UnityEngine;

public class CSVWriter : MonoBehaviour
{
    private StreamWriter streamWriter;

    private string filePath;
    private float flushInterval = 5.0f; // 一定時間ごとにFlush
    private float lastFlushTime = 0f;

    bool isRecording = true;

    [SerializeField] private DataProcessor dataProcessor;

    private DateTime last_sensor_time; // 前回のセンサーデータ取得時刻


    void Start(){
        string fileName = "appdata_" + System.DateTime.Now.ToString("yyMMdd_HHmmss") + ".csv";

        string directoryPath = Path.Combine(Application.persistentDataPath, "csv");
        Directory.CreateDirectory(directoryPath);
        filePath = Path.Combine(directoryPath, fileName);
        Debug.Log(filePath);

        streamWriter = new StreamWriter(filePath, true);
        // ヘッダー
        streamWriter.WriteLine("sensor_time, display_time, delay, handle_angvel, vehicle_angvel, handle_pure_angvel, vehicle_angvel_moving_average, vehicle_angle, vehicle_lateral_accel, suppress_direction, display_state");

        // フレームごとに監視を開始
        StartCoroutine(RecordData());
    }


    IEnumerator RecordData(){
        while (isRecording){
            yield return new WaitForEndOfFrame(); // フレーム描画完了を待つ

            DateTime display_time = DateTime.Now;
            DateTime sensor_time = dataProcessor.GetSensorTime();

            if(last_sensor_time != sensor_time){
                double delay = (display_time - sensor_time).TotalMilliseconds;
                string param_string = dataProcessor.GetParamString();
    
                streamWriter.WriteLine($"{sensor_time:HH:mm:ss.fff}, {display_time:HH:mm:ss.fff}, {delay}, {param_string}");
                
                last_sensor_time = sensor_time;
            }

            // 定期的にファイルをFlushする
            if (Time.time - lastFlushTime >= flushInterval){
                streamWriter.Flush();
                lastFlushTime = Time.time;
            }
        }
    }


    void CloseWriter(){
        if (streamWriter == null) return;

        isRecording = false;
        StopAllCoroutines();
        streamWriter.Flush();
        streamWriter.Close();
        streamWriter = null;
    }


    private void OnDestroy(){
        CloseWriter();
    }


    public void ForceClose(){
        CloseWriter();
    }


    void OnApplicationQuit(){
        CloseWriter();
    }

}
