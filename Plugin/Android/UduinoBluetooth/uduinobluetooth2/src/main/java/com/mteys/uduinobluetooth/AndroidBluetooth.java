package com.mteys.uduinobluetooth;

import java.util.List;
import java.util.ArrayList;
import java.util.Arrays;

import java.util.HashMap;
import java.util.Map;
import java.util.UUID;

import android.app.Fragment;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.Context;
import android.content.ServiceConnection;
import android.content.BroadcastReceiver;
import android.content.ComponentName;
import android.content.pm.PackageManager;

import android.os.Debug;
import android.os.IBinder;
import android.os.Bundle;
import android.os.Handler;

import android.util.Log;
import com.unity3d.player.UnityPlayer;

import android.location.LocationManager;
import android.bluetooth.BluetoothManager;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothGattCharacteristic;
import android.bluetooth.BluetoothGattService;
import org.json.JSONObject;
import org.json.JSONArray;
import org.json.JSONException;
import android.app.Activity;
import android.widget.Toast;

/**
 * Created by Fenix on 24/09/2017.
 */

public class AndroidBluetooth extends Fragment {
    private Activity _unityActivity;
    /*
    Singleton instance.
    */
    private static AndroidBluetooth instance = null;

    /*
    Definition of the BLE Unity message methods used to communicate back with Unity.
    */
    public static final String BLEUnityMessageName_OnBleDidInitialize = "OnBleDidInitialize";
    public static final String BLEUnityMessageName_OnBleDidConnect = "OnBleDidConnect";
    public static final String BLEUnityMessageName_OnBleDidCompletePeripheralScan = "PeripheralScanComplete";
    public static final String BLEUnityMessageName_OnBleDidDisconnect = "OnBleDidDisconnect";
    public static final String BLEUnityMessageName_OnBleDidReceiveData = "OnBleDidReceiveData";

    /*
    Static variables
    */
    private static final String TAG = "ANDROID_BLUETOOTH";
    private static final int REQUEST_ENABLE_BT = 1;
    private int scanDuration = 3000;
    public static final int REQUEST_CODE = 30;

    /*
    List containing all the discovered bluetooth devices
    */
    private List<BluetoothDevice> _mDevice = new ArrayList<BluetoothDevice>();

    /*
    The latest received data
    */
    private byte[] _dataRx = new byte[3];

    /*
    Bluetooth service
    */
    private RBLService _mBluetoothLeService;


    private Map<UUID, BluetoothGattCharacteristic> _map = new HashMap<UUID, BluetoothGattCharacteristic>();

    /*
    Bluetooth adapter
    */
    private BluetoothAdapter _mBluetoothAdapter;

    /*
    Bluetooth device address and name to which the app is currently connected
    */
    private BluetoothDevice _device;
    private String _mDeviceAddress;
    private String _mDeviceName;

    /*
    Boolean variables used to estabilish the status of the connection
    */
    private boolean _connState = false;
    private boolean _searchingDevice = false;
    private boolean serviceRunning = false;

    /*
    Link with Unity
     */
    String gameObjectName;


    /*
    Data to send
     */
    DataQueues packetsToSend = new DataQueues();

    public static void start(String gameObjectName)
    {
        // Instantiate and add to Unity Player Activity.
        if(AndroidBluetooth.instance  == null) {
            instance = new AndroidBluetooth();
            instance.gameObjectName = gameObjectName; // Store 'GameObject' reference
            UnityPlayer.currentActivity.getFragmentManager().beginTransaction().add(instance, AndroidBluetooth.TAG).commit();
            instance._unityActivity = UnityPlayer.currentActivity;
            instance._InitBLE();
        }
    }

    //Debug in toast message
    public void Debug(String message) {
        Toast.makeText(this.getActivity().getBaseContext(), message, Toast.LENGTH_LONG).show();
    }

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setRetainInstance(true); // Retain between configuration changes (like device rotation)
        Debug("onCreate");
        DebugUnity("onCreate");
    }

    public void DebugUnity(String message) {
        UnityPlayer.UnitySendMessage(gameObjectName, "CallUnityEvent",message);
    }


    /*
    The service connection containing the actions definition onServiceConnected and onServiceDisconnected
    */
    private final ServiceConnection _mServiceConnection = new ServiceConnection()
    {
        @Override
        public void onServiceConnected(ComponentName componentName, IBinder service)
        {
            _mBluetoothLeService = ((RBLService.LocalBinder) service).getService();

            if (!_mBluetoothLeService.initialize())
            {
                Log.e(TAG, "onServiceConnected: Unable to initialize Bluetooth");
                //finish();
            } else {
                Log.d(TAG, "onServiceConnected: Bluetooth initialized correctly");
                _mBluetoothLeService.connect(_mDeviceAddress);
            }
        }

        @Override
        public void onServiceDisconnected(ComponentName componentName)
        {
            Log.d(TAG, "onServiceDisconnected: Bluetooth disconnected");
            _mBluetoothLeService = null;
        }
    };

    /*
    Callback called when the scan of bluetooth devices is finished
    */
    private BluetoothAdapter.LeScanCallback _mLeScanCallback = new BluetoothAdapter.LeScanCallback()
    {
        @Override
        public void onLeScan(final BluetoothDevice device, final int rssi, byte[] scanRecord)
        {
            _unityActivity.runOnUiThread(new Runnable() {
                @Override
                public void run()
                {
                    Log.d(TAG, "onLeScan: run()");
                    if (device != null && device.getName() != null)
                    {
                        Log.d(TAG, "onLeScan: device is not null");
                        if (_mDevice.indexOf(device) == -1)
                        {
                            Log.d(TAG, "onLeScan: add device to _mDevice");
                            _mDevice.add(device);

                            Log.d(TAG, "Adress " +  device.getAddress());
                            Log.d(TAG, "Adress " +  device.getName());
                        }
                    }
                    else
                    {
                        Log.e(TAG, "onLeScan: device is null");
                    }
                }
            });
        }
    };



    public void SendUnityArduinoMessage(String message) {
        UnityPlayer.UnitySendMessage(instance.gameObjectName, "PluginMessageReceived",message);
    }

    String inBuffer = "";
    int maxBufferLength = 150;

    private void AddToInBuffer(String string) {
        inBuffer += string;
        String[] lines = inBuffer.split("\\r\\n|\\n|\\r");
        Log.d(TAG, "Add to in buffer " + string  +  " length:" + string.length());
        Log.d(TAG, "In Buffer " + inBuffer);
        byte[] bytes = inBuffer.getBytes();
        for(int i=0; i< bytes.length;i++)
            Log.d(TAG, "buff " + bytes[i]);

        if(lines.length > 1) {
            for(int i=0;i <lines.length -1; i++ ) {
                if(lines[i] != null && !lines[i].isEmpty()) {
                    SendUnityArduinoMessage(lines[i]);
                    Log.d(TAG, "heeere " + lines[i]);
                }
            }
            inBuffer = "";
            inBuffer = lines[lines.length -1];
        } else {
         //   DebugUnity("finishWith " + inBuffer.endsWith("\\r\\n|\\n|\\r"));

            if(lines.length > 0  && inBuffer.endsWith("\n") || inBuffer.length() > maxBufferLength ) {
                SendUnityArduinoMessage(lines[0]);
                Log.d(TAG, "one " + lines[0]);

                inBuffer = "";
            }
        }
    }


    /*
    Callback called when the bluetooth device receive relevant updates about connection, disconnection, service discovery, data available, rssi update
    */
    private final BroadcastReceiver _mGattUpdateReceiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            final String action = intent.getAction();

            if (RBLService.ACTION_GATT_CONNECTED.equals(action))
            {
                _connState = true;

                Log.d(TAG, "Connection estabilished with: " + _mDeviceAddress);
                //startReadRssi();
            }
            else if (RBLService.ACTION_GATT_DISCONNECTED.equals(action))
            {
                _connState = false;
                UnityPlayer.UnitySendMessage(instance.gameObjectName, BLEUnityMessageName_OnBleDidDisconnect, "Success");
                _mBluetoothLeService.disconnect();
                packetsToSend = null;
                packetsToSend  = new DataQueues();
                Log.d(TAG, "Connection lost");
            }
            else if (RBLService.ACTION_GATT_SERVICES_DISCOVERED.equals(action))
            {
                Log.d(TAG, "Service discovered! Registering GattService ACTION_GATT_SERVICES_DISCOVERED");
                getGattService(_mBluetoothLeService.getSupportedGattService());

                Log.d(TAG, "Send BLEUnityMessageName_OnBleDidConnect success signal to Unity");
                UnityPlayer.UnitySendMessage(instance.gameObjectName, BLEUnityMessageName_OnBleDidConnect, "Success");
            }
            else if (RBLService.ACTION_DATA_AVAILABLE.equals(action))
            {
                Log.d(TAG, "New Data received by the server");
                _dataRx = intent.getByteArrayExtra(RBLService.EXTRA_DATA);

                // TODO : Ici mettre ça dans une partie qui recevoit comme android serial
                try {
                    String str = new String(_dataRx, "UTF-8");
                    AddToInBuffer(str);
                }  catch( Exception e) {
                    Log.d(TAG, "Error when parsing the received string " + e);
                }
            }
            else if (RBLService.ACTION_GATT_RSSI.equals(action))
            {
                String rssiData = intent.getStringExtra(RBLService.EXTRA_DATA);
                Log.d(TAG, "RSSI: " + rssiData);
            }
            else if (RBLService.ACTION_DATA_WRITE.equals(action))
            {
                Log.d(TAG, "Characteristc data written.");

                if(packetsToSend.getArrSize() > 0)
                    SendToBluetooth(packetsToSend.popQueue());
            }


        }
    };

    /*
    METHODS DEFINITION
    */

    public static AndroidBluetooth getInstance(Activity activity)
    {
        if (instance == null )
        {
            synchronized (AndroidBluetooth.class)
            {
                if (instance == null)
                {
                    Log.d(TAG, "BleFramework: Creation of _instance");
                    instance = new AndroidBluetooth();
                }
            }
        }

        return instance;
    }

    /*
    public AndroidBluetooth(Activity activity)
    {
        Log.d(TAG, "BleFramework: saving unityActivity in private var.");
        this._unityActivity = activity;
    }
*/
    /*
    Method used to create a filter for the bluetooth actions that you like to receive
    */
    private static IntentFilter makeGattUpdateIntentFilter()
    {
        final IntentFilter intentFilter = new IntentFilter();

        intentFilter.addAction(RBLService.ACTION_GATT_CONNECTED);
        intentFilter.addAction(RBLService.ACTION_GATT_DISCONNECTED);
        intentFilter.addAction(RBLService.ACTION_GATT_SERVICES_DISCOVERED);
        intentFilter.addAction(RBLService.ACTION_DATA_AVAILABLE);
        intentFilter.addAction(RBLService.ACTION_DATA_WRITE);
        //intentFilter.addAction(RBLService.ACTION_GATT_RSSI);

        return intentFilter;
    }


    /*
    Method used to initialize the characteristic for data transmission
    */

    private void getGattService(BluetoothGattService gattService)
    {

        if (gattService == null)
            return;
        Log.d(TAG, "getGattService: Getting Gatt");


        BluetoothGattCharacteristic characteristic = gattService.getCharacteristic(RBLService.UUID_BLE_SHIELD_TX);
        Log.d(TAG, "getGattService Here");

        if(characteristic != null) {
            _map.put(characteristic.getUuid(), characteristic);

            BluetoothGattCharacteristic characteristicRx = gattService.getCharacteristic(RBLService.UUID_BLE_SHIELD_RX);
            _mBluetoothLeService.setCharacteristicNotification(characteristicRx, true);
            _mBluetoothLeService.readCharacteristic(characteristicRx);
        } else {
            Log.e(TAG,"Char, nul");
        }

    }


    /*
    Method used to scan for available bluetooth low energy devices
    */
    private void scanLeDevice()
    {
        new Thread()
        {
            @Override
            public void run()
            {
                _searchingDevice = true;
                Log.d(TAG, "scanLeDevice: _mBluetoothAdapter StartLeScan");
                _mBluetoothAdapter.startLeScan(_mLeScanCallback);

                try
                {
                    Log.d(TAG, "scanLeDevice: scan for " + scanDuration + " seconds then abort");
                    Thread.sleep(scanDuration);
                }
                catch (InterruptedException e)
                {
                    Log.d(TAG, "scanLeDevice: InterruptedException");
                    e.printStackTrace();
                }

                Log.d(TAG, "scanLeDevice: _mBluetoothAdapter StopLeScan");
                _mBluetoothAdapter.stopLeScan(_mLeScanCallback);
                _searchingDevice = false;
                Log.d(TAG, "scanLeDevice: _mDevice size is " + _mDevice.size());

                UnityPlayer.UnitySendMessage(instance.gameObjectName, BLEUnityMessageName_OnBleDidCompletePeripheralScan, _GetListOfDevices());
            }
        }.start();
    }


    private void unregisterBleUpdatesReceiver()
    {
        Log.d(TAG,"unregisterBleUpdatesReceiver:");
        _unityActivity.unregisterReceiver(_mGattUpdateReceiver);
    }

    private void registerBleUpdatesReceiver()
    {
        Log.d(TAG,"registerBleUpdatesReceiver:");
        if (!_mBluetoothAdapter.isEnabled())
        {
            Log.d(TAG,"registerBleUpdatesReceiver: WARNING: _mBluetoothAdapter is not enabled!");
            /*
            Intent enableBtIntent = new Intent(
                    BluetoothAdapter.ACTION_REQUEST_ENABLE);
            startActivityForResult(enableBtIntent, REQUEST_ENABLE_BT);
            */
        }
        Log.d(TAG,"registerBleUpdatesReceiver: registerReceiver");
        _unityActivity.registerReceiver(_mGattUpdateReceiver, makeGattUpdateIntentFilter());
    }

    /*
    Public methods that can be directly called by Unity
    */
    public void _InitBLE()
    {
        System.out.println("Android Executing: _InitBLE");

        if (!_unityActivity.getPackageManager().hasSystemFeature(PackageManager.FEATURE_BLUETOOTH_LE))
        {
            Log.d(TAG,"Missing FEATURE_BLUETOOTH_LE");
            UnityPlayer.UnitySendMessage(instance.gameObjectName, BLEUnityMessageName_OnBleDidInitialize, "Missing FEATURE_BLUETOOTH_LE. Are you sure your phone support BLE ? ");
            //finish();
            return;
        }

        final BluetoothManager mBluetoothManager = (BluetoothManager) _unityActivity.getSystemService(Context.BLUETOOTH_SERVICE);
        _mBluetoothAdapter = mBluetoothManager.getAdapter();
        if (_mBluetoothAdapter == null)
        {
            Log.d(TAG,"onCreate: fail: _mBluetoothAdapter is null");
            UnityPlayer.UnitySendMessage(instance.gameObjectName, BLEUnityMessageName_OnBleDidInitialize, "Missing Context.BLUETOOTH_SERVICE. Are you sure the bluetooth is started ?");
            //finish();
            return;
        }

        if(!_mBluetoothAdapter.isEnabled()) {
            Log.d(TAG,"onCreate: fail: _mBluetoothAdapter is not enabled");
            UnityPlayer.UnitySendMessage(instance.gameObjectName, BLEUnityMessageName_OnBleDidInitialize, "Bluetooth is not enabled.");
            //finish();
            return;
        }

        //TODO : http://developer.radiusnetworks.com/2015/09/29/is-your-beacon-app-ready-for-android-6.html
        LocationManager locationManager = (LocationManager) _unityActivity.getSystemService(Context.LOCATION_SERVICE);
        if(!locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER) && !locationManager.isProviderEnabled(LocationManager.NETWORK_PROVIDER)) {
            //All location services are disabled
            Log.d(TAG,"onCreate: fail: _location is not enabled");
            UnityPlayer.UnitySendMessage(instance.gameObjectName, BLEUnityMessageName_OnBleDidInitialize, "Location needs to be enabled to detect BLE devices.");
            //finish();
            return;
        }

        registerBleUpdatesReceiver();

        Log.d(TAG,"onCreate: _mBluetoothAdapter correctly initialized");
        UnityPlayer.UnitySendMessage(instance.gameObjectName, BLEUnityMessageName_OnBleDidInitialize, "Success");

    }


    public void _ScanForPeripherals(int duration)
    {
        scanDuration = duration;
        Log.d(TAG, "_ScanForPeripherals: Launching scanLeDevice");
        scanLeDevice();
    }

    public boolean _IsDeviceConnected()
    {
        Log.d(TAG,"_IsDeviceConnected");
        return _connState;
    }

    public boolean _SearchDeviceDidFinish()
    {
        Log.d(TAG,"_SearchDeviceDidFinish");
        return !_searchingDevice;
    }

    public String _GetListOfDevices()
    {
        String jsonListString;

        if (_mDevice.size() > 0)
        {
            Log.d(TAG,"_GetListOfDevices");

            JSONObject dataDevices = new JSONObject();
            JSONArray  devices = new JSONArray ();

            for (int i = 0; i < _mDevice.size(); i++)
            {

                BluetoothDevice bd = _mDevice.get(i);
                JSONObject currentDevice = new JSONObject();

                try
                {
                    Log.d(TAG, "_GetListOfDevices: Try inserting uuuidsJSON array in the JSONObject");
                    currentDevice.put("uuid", bd.getAddress());
                    currentDevice.put("name", bd.getName());
                    devices.put(currentDevice);
                }
                catch (JSONException e)
                {
                    Log.e(TAG, "_GetListOfDevices: JSONException");
                    e.printStackTrace();
                }
            }

            try
            {
                dataDevices.put("devices", devices);
            }
            catch (JSONException e)
            {
                Log.e(TAG, "_GetListOfDevices: JSONException");
                e.printStackTrace();
            }

            jsonListString = dataDevices.toString();
            Log.d(TAG, "_GetListOfDevices: sending found devices in JSON: " + jsonListString);
        }
        else
        {
            jsonListString = "NO DEVICE FOUND";
            Log.d(TAG, "_GetListOfDevices: no device was found");
        }

        return jsonListString;
    }

    /*
    // We don't use this one
    public boolean _ConnectPeripheralAtIndex(int peripheralIndex)
    {
        Log.d(TAG,"_ConnectPeripheralAtIndex: " + peripheralIndex);
        BluetoothDevice device = _mDevice.get(peripheralIndex);

        _mDeviceAddress = device.getAddress();
        _mDeviceName = device.getName();

        Intent gattServiceIntent = new Intent(_unityActivity, RBLService.class);
        _unityActivity.bindService(gattServiceIntent, _mServiceConnection, _unityActivity.BIND_AUTO_CREATE);
        return true;
    }
    */

    public boolean _ConnectPeripheral(String peripheralID)
    {
        inBuffer = "";

        Log.d(TAG,"_ConnectPeripheral: " + peripheralID);

        for (BluetoothDevice device : _mDevice)
        {
            if (device.getAddress().equals(peripheralID))
            {
                _mDeviceAddress = device.getAddress();
                _mDeviceName = device.getName();
                Log.d(TAG,"Trying to connect to : " + _mDeviceName);

                try {
                    Intent gattServiceIntent = new Intent(_unityActivity, RBLService.class);
                   serviceRunning = _unityActivity.bindService(gattServiceIntent, _mServiceConnection, _unityActivity.BIND_AUTO_CREATE);
                    Log.d(TAG,"Connecting... " + device.getUuids());

                    UnityPlayer.UnitySendMessage(instance.gameObjectName, BLEUnityMessageName_OnBleDidConnect, "Success");
                }catch(Exception e) {
                    Log.d(TAG,"Error : " + e);
                    return false;
                }
                return true;
            }
        }
        Log.e(TAG,"here" + (_mDevice == null));


        return false;
    }

    public boolean _Disconnect()
    {
        if(_mBluetoothLeService != null)
            _mBluetoothLeService.disconnect();
        if(serviceRunning) {
            try {
            _unityActivity.unbindService(_mServiceConnection);
            serviceRunning = false;
            } catch(Exception e) {
                 Log.d(TAG, "" + e);
            }
        }

        _device = null;
        _mDeviceAddress = null;
        _mDeviceName = null;

        inBuffer = "";

        return true;
    }

    public String _GetData()
    {
        Log.d(TAG,"_GetData: ");
        try {
            String str = new String(_dataRx, "UTF-8");
            return str;

        }  catch( Exception e) {
            Log.d(TAG, "Error when parsing the received string " + e);
        }
        return "";
    }

    public void _SendData(String message)
    {
        Log.d(TAG,"_SendData: " + message);

        int chunksize = 20;
        int len = message.length();
        for (int i=0; i<len; i+=chunksize)
        {
            String parsedMessage  = (message.substring(i, Math.min(len, i + chunksize)));
            try {
                byte[] bytes = parsedMessage.getBytes("UTF-8");
                packetsToSend.addToQueue(bytes);
                Log.d(TAG, "Adding to queue : " + parsedMessage);

            } catch(Exception e) {
                Log.d(TAG, "_error " + e);
            }
        }

        if(packetsToSend.getArrSize() > 0)
            SendToBluetooth(packetsToSend.popQueue());
    }

    public void SendToBluetooth(byte[] messagesInBytes) {

        BluetoothGattCharacteristic characteristic = _map.get(RBLService.UUID_BLE_SHIELD_TX);
        // characteristic = _map.get(RBLService.UUID_BLE_SHIELD_RX);

        if(characteristic != null ) {
            characteristic.setWriteType(BluetoothGattCharacteristic.WRITE_TYPE_NO_RESPONSE);
            characteristic.setValue(messagesInBytes);

            if (_mBluetoothLeService==null)
            {
                Debug("_mBluetoothLeService is null");
                Log.d(TAG, "_mBluetoothLeService is null");
            }
            Log.d(TAG, "writing char : " + characteristic );

            _mBluetoothLeService.writeCharacteristic(characteristic);
        } else {
            Debug("characteristic is null !");
        }
    }
}

class DataQueues {

    private ArrayList<byte[]> queueArray;
    private int queuSize;

    protected DataQueues() {
        queueArray = new ArrayList<>();
    }
    protected void addToQueue(byte[] bytesArr) {
        queueArray.add(bytesArr);
    }

    protected byte[] popQueue() {
        if (queueArray.size() >= 0)
            return queueArray.remove(0);
        else {
            return null;
        }
    }
    protected int getArrSize() {
        return queueArray.size();
    }
}
