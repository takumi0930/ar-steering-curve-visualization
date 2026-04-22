using UnityEngine;
using System;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.SpatialManipulation;

public class DataProcessor : MonoBehaviour
{
    [SerializeField] private GameObject right_bar;
    [SerializeField] private GameObject left_bar;

    private DateTime sensor_time; // センサーデータ取得時刻
 
    private float handle_angvel = 0f; // ハンドル角速度
    private float vehicle_angvel = 0f; // 車角速度
    private float handle_pure_angvel = 0f; // ハンドルの純粋な角速度
    private float vehicle_angvel_moving_average = 0f; // 車角速度の移動平均
    private float vehicle_angle = 0f; // 車角度
    private float vehicle_lateral_accel = 0f; // 車の横加速度

    [SerializeField] private float ANGVEL_THRESHOLD = 50f; // 角速度の閾値
    [SerializeField] private int MAX_COUNT = 10; // 記録する履歴の件数
    [SerializeField] private int DETECTION_COUNT = 4; // 連続検出回数
    [SerializeField] private float START_THRESHOLD = 10f; // 車角速度の移動平均における抑制開始閾値
    [SerializeField] private float RELEASE_THRESHOLD = 2f; // 車角速度の移動平均における抑制解除閾値
    [SerializeField] private int ANGVEL_MAX_COUNT = 16; // 記録する履歴の件数
    [SerializeField] private int ANGVEL_DETECTION_COUNT = 10; // 移動平均を取る個数

    private enum DisplayState { Left = -1, None = 0, Right = 1 } // 表示状態
    private DisplayState display_state = DisplayState.None; // 表示状態 左-1 非表示0 右1
    private DisplayState previous_display_state = DisplayState.None; // 前の表示状態 左-1 非表示0 右1
  
    private List<float> vehicle_angvel_history = new List<float>(); // 角角速度の履歴
    private List<DisplayState> display_state_history = new List<DisplayState>(); // 表示状態の履歴
    DisplayState suppress_direction = DisplayState.None;   // 抑制方向 右抑制=1, 左抑制=-1, 抑制なし=0

    [SerializeField] private MeshRenderer rightRenderer;
    [SerializeField] private MeshRenderer leftRenderer;
    private Material right_bar_mat;
    private Material left_bar_mat;
    static readonly int ColorID = Shader.PropertyToID("_Color");
    static readonly Color32 colorBright = new Color32(29, 161, 242, 255); // 表示
    static readonly Color32 colorZero = new Color32(29, 161, 242, 0); // 非表示

    private string last_data_body = ""; // 前回データ内容

    // 初期化関数
    private void Start(){
        if (rightRenderer == null || leftRenderer == null){
            Debug.LogError("MeshRenderer not found");
            return;
        }

        right_bar_mat = rightRenderer.material;
        left_bar_mat = leftRenderer.material;
    }


    // センサーデータを受信するたびに呼び出される中核関数
    public void ProcessReceivedData(string data){
        bool isNewData = StoreReceivedData(data);
        if (!isNewData) return;
        
        UpdateBarDisplay();
        UpdateSuppression();
    }


    // 受信データから変数に格納する関数
    private bool StoreReceivedData(string data){
        string[] parts = data.Split(' ');

        // 処理可能データか確認する
        if(parts.Length != 5) return false;

        // 同一センサーデータか確認する
        string data_body = parts[1] + " " + parts[2] + " " + parts[3] + " " + parts[4];
        if (data_body == last_data_body) return false;
        last_data_body = data_body;
        
        // センサーデータ取得時刻
        if (!DateTime.TryParseExact(parts[0], "HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out sensor_time))
        return false;
        // ハンドル角速度
        if (!float.TryParse(parts[1], out float part1)) return false;
        handle_angvel = -part1;
        // 車角速度
        if (!float.TryParse(parts[2], out float part2)) return false;
        vehicle_angvel = -part2; 
        // 車角度
        if (!float.TryParse(parts[3], out float part3)) return false;
        vehicle_angle = -part3;
        // 車横加速度
        if (!float.TryParse(parts[4], out float part4)) return false;
        vehicle_lateral_accel = -part4;

        handle_pure_angvel = handle_angvel - vehicle_angvel;

        // 車角速度の履歴に追加する
        vehicle_angvel_history.Add(vehicle_angvel);
        if (vehicle_angvel_history.Count > ANGVEL_MAX_COUNT){
            vehicle_angvel_history.RemoveAt(0);
        }   

        return true;
    }


    // 折れ線UIの表示状態を変更し描画を行う関数
    private void UpdateBarDisplay(){

        // ハンドル角速度により表示状態を設定する
        display_state =
        handle_pure_angvel >= ANGVEL_THRESHOLD ? DisplayState.Right :
        handle_pure_angvel <= -ANGVEL_THRESHOLD ? DisplayState.Left :
        DisplayState.None;

        // 抑制方向の表示は抑制する
        if (suppress_direction != DisplayState.None){ // 抑制方向がある場合
            // 今回判定のdisplay_stateが抑制方向であれば
            if (display_state == suppress_direction){
                display_state = DisplayState.None; // 表示を抑制する
            }
        }

        // 表示状態に変更があれば，表示を変更する．
        if(display_state != previous_display_state){
            // 表示状態に応じて、色を変更する
            switch(display_state){
                case DisplayState.Right:
                    right_bar_mat.SetColor(ColorID, colorBright);
                    left_bar_mat.SetColor(ColorID, colorZero);
                    break;

                case DisplayState.Left:
                    right_bar_mat.SetColor(ColorID, colorZero);
                    left_bar_mat.SetColor(ColorID, colorBright);
                    break;

                case DisplayState.None:
                    right_bar_mat.SetColor(ColorID, colorZero);
                    left_bar_mat.SetColor(ColorID, colorZero);
                    break;
                
                default:
                    break;
            } 
            // 表示状態を更新する
            previous_display_state = display_state;
        }
        
        // 表示状態を履歴に記録する
        display_state_history.Add(display_state);
        if (display_state_history.Count > MAX_COUNT){
            display_state_history.RemoveAt(0);
        }

    }


    // 表示抑制を制御する関数
    private void UpdateSuppression(){

        // 履歴の要素数が4に満たないはreturn
        if(display_state_history.Count < DETECTION_COUNT) return;

        // 車角速度の移動平均
        vehicle_angvel_moving_average = GetMovingAverage();

        // 表示抑制を制御する
        if(suppress_direction == DisplayState.None){
            // 抑制開始, ハンドル回転を連続検出中に、車角速度の移動平均が閾値以上ならば、反対側を抑制開始
            // ハンドル回転の連続検出方向
            DisplayState consecutive = GetConsecutiveDirection(display_state_history);
            if (consecutive == DisplayState.Right && vehicle_angvel_moving_average >= START_THRESHOLD){
                suppress_direction = DisplayState.Left;
            }
            else if(consecutive == DisplayState.Left && vehicle_angvel_moving_average <= -START_THRESHOLD){
                suppress_direction = DisplayState.Right;
            }
        }else{
            // 抑制解除, 表示抑制中に，車角速度の移動平均が閾値以下になれば，抑制解除
            if(Mathf.Abs(vehicle_angvel_moving_average) <= RELEASE_THRESHOLD){
                suppress_direction = DisplayState.None;
            }
        }

    }


    // 連続して検出した方向を返す関数
    DisplayState GetConsecutiveDirection(List<DisplayState> displayHistory){
        int history_count = displayHistory.Count;

        if (history_count < DETECTION_COUNT)
            return DisplayState.None;

        int start = history_count - DETECTION_COUNT;
        DisplayState first = displayHistory[start];

        if (first == DisplayState.None)
            return DisplayState.None;

        for (int i = start + 1; i < history_count; i++){
            if (displayHistory[i] != first)
                return DisplayState.None;
        }

        return first;
    }


    // 車角速度の移動平均を返す関数
    float GetMovingAverage(){
        int history_count = vehicle_angvel_history.Count;

        if (history_count < ANGVEL_DETECTION_COUNT)
            return 0f;
        
        int start = history_count - ANGVEL_DETECTION_COUNT;
        float sum = 0f;
        for (int i = start; i < history_count; i++){
            sum += vehicle_angvel_history[i];
        }

        float average = sum / ANGVEL_DETECTION_COUNT;
        return average;
    }


    public DateTime GetSensorTime(){
        return sensor_time;
    }


    public string GetParamString(){
        return $"{handle_angvel}, {vehicle_angvel}, {handle_pure_angvel}, {vehicle_angvel_moving_average}, {vehicle_angle}, {vehicle_lateral_accel},  {(int)suppress_direction}, {(int)display_state}";
    }


    private void OnDestroy(){
        if (right_bar_mat != null) Destroy(right_bar_mat);
        if (left_bar_mat != null) Destroy(left_bar_mat);
    }

}