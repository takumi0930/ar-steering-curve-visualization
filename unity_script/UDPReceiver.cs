using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


public class UDPReceiver : MonoBehaviour{
    [SerializeField] private DataProcessor dataProcessor;

    private const int port = 9000;
    private UdpClient udpClient;
    private bool isServerRunning = false;
    private Task serverTask;

    private readonly ConcurrentQueue<string> receivedQueue = new ConcurrentQueue<string>();


    private void Start(){
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        if (dataProcessor == null){
            Debug.LogError("DataProcessor is not assigned");
        }
        
        // 非同期サーバー開始
        serverTask = StartServerAsync();
    }


    private void Update(){
        // 受信キューからデータを取り出して処理させる
        while (receivedQueue.TryDequeue(out string data)){
            if (dataProcessor != null){
                dataProcessor.ProcessReceivedData(data);
            }
        }
    }


    private async Task StartServerAsync(){
        if (isServerRunning)
            return;

        udpClient = new UdpClient(port);
        isServerRunning = true;
        Debug.Log("UDPReceiver started on port " + port);

        while (isServerRunning){
            try{
                UdpReceiveResult result = await udpClient.ReceiveAsync();
                string receivedData = Encoding.ASCII.GetString(result.Buffer);
                // 受信キューにデータを格納する
                receivedQueue.Enqueue(receivedData);
            }
            catch (ObjectDisposedException){
                break; // 終了処理
            }
            catch (SocketException){
                // Close時やネットワークエラー
                if (!isServerRunning) break;
                Debug.LogWarning("SocketException occurred during UDP receive");
            }
            catch (Exception e){
                Debug.LogError("UDP receive error: " + e);
            }
        }

        Debug.Log("UDPReceiver stopped");
    }


    public async Task StopServerAsync(){
        if (!isServerRunning) return;

        isServerRunning = false;

        udpClient?.Close();

        if (serverTask != null && !serverTask.IsCompleted){
            await serverTask;
        }

        udpClient = null;
    }


    private void OnApplicationQuit(){
        if (isServerRunning){
            isServerRunning = false;
            udpClient?.Close();
        }
    }

}
