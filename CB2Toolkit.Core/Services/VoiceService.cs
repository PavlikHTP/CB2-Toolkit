using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NAudio.Wave;
using Concentus.Enums;
using Concentus.Structs;

namespace CB2Toolkit.Core.Services;

public class VoiceService
{
    private static readonly Lazy<VoiceService> _instance = new(() => new VoiceService());
    public static VoiceService Instance => _instance.Value;

    private UdpClient? _udpClient;
    private IPEndPoint? _targetEndPoint;
    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _waveProvider;
    private OpusEncoder? _encoder;
    private OpusDecoder? _decoder;
    private bool _isCurrentCallActive;
    
    private short[]? _pcmBuffer;
    private byte[]? _compressedBuffer;
    private byte[]? _receiveBuffer;
    private short[]? _outPcmBuffer;
    private byte[]? _rawBytesBuffer;

    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public float MicVolumeMultiplier { get; set; } = 1.0f;

    public event Action<float>? OnRemoteVolumeChanged;
    public event Action<float>? OnLocalVolumeChanged;

    private VoiceService() { }

    public void StartCall(string targetIp, int localPort, int targetPort, int inputDeviceIndex, int outputDeviceIndex)
    {
        if (_isCurrentCallActive) return;

        try
        {
            _targetEndPoint = new IPEndPoint(IPAddress.Parse(targetIp), targetPort);
            
            _udpClient = new UdpClient(localPort);
            _udpClient.Connect(_targetEndPoint); 

            _encoder = new OpusEncoder(48000, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            _decoder = new OpusDecoder(48000, 1);
            
            _pcmBuffer = new short[960];
            _compressedBuffer = new byte[1275];
            _receiveBuffer = new byte[1275];
            _outPcmBuffer = new short[960];
            _rawBytesBuffer = new byte[1920];

            _waveProvider = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
            {
                DiscardOnBufferOverflow = true
            };

            _waveOut = new WaveOutEvent
            {
                DeviceNumber = outputDeviceIndex
            };
            _waveOut.Init(_waveProvider);
            _waveOut.Play();

            _waveIn = new WaveInEvent
            {
                DeviceNumber = inputDeviceIndex,
                BufferMilliseconds = 20,
                WaveFormat = new WaveFormat(48000, 16, 1)
            };
            _waveIn.DataAvailable += OnAudioDataAvailable;
            _waveIn.StartRecording();

            _isCurrentCallActive = true;
            Task.Run(ReceiveAudioLoop);
        }
        catch
        {
            StopCall();
            throw;
        }
    }

    public void StopCall()
    {
        _isCurrentCallActive = false;

        try { _waveIn?.StopRecording(); } catch { }
        try { _waveIn?.Dispose(); } catch { }
        _waveIn = null;

        try { _waveOut?.Stop(); } catch { }
        try { _waveOut?.Dispose(); } catch { }
        _waveOut = null;
        _waveProvider = null;

        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;

        _encoder = null;
        _decoder = null;
        
        _pcmBuffer = null;
        _compressedBuffer = null;
        _receiveBuffer = null;
        _outPcmBuffer = null;
        _rawBytesBuffer = null;

        OnRemoteVolumeChanged?.Invoke(0);
        OnLocalVolumeChanged?.Invoke(0);
    }

    private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_isCurrentCallActive || _udpClient == null || _encoder == null || _pcmBuffer == null || _compressedBuffer == null) return;
        
        Buffer.BlockCopy(e.Buffer, 0, _pcmBuffer, 0, e.BytesRecorded);

        float localPeak = 0;
        float mult = MicVolumeMultiplier;
        
        for (int i = 0; i < _pcmBuffer.Length; i++)
        {
            if (mult != 1.0f)
            {
                int sample = (int)(_pcmBuffer[i] * mult);
                if (sample > 32767) sample = 32767;
                else if (sample < -32768) sample = -32768; 
                _pcmBuffer[i] = (short)sample;
            }

            float val = Math.Abs(_pcmBuffer[i] / 32768f);
            if (val > localPeak) localPeak = val;
        }

        OnLocalVolumeChanged?.Invoke(IsMuted ? 0 : localPeak);

        if (IsMuted) return;
        
        int encodedBytes = _encoder.Encode(_pcmBuffer, 0, 960, _compressedBuffer, 0, _compressedBuffer.Length);

        try
        {
            _udpClient.Send(_compressedBuffer, encodedBytes);
        }
        catch { }
    }

    private async Task ReceiveAudioLoop()
    {
        while (_isCurrentCallActive && _udpClient != null && _decoder != null && _waveProvider != null && 
               _receiveBuffer != null && _outPcmBuffer != null && _rawBytesBuffer != null)
        {
            try
            {
                int receivedBytes = await _udpClient.Client.ReceiveAsync(new ArraySegment<byte>(_receiveBuffer), SocketFlags.None);
                if (receivedBytes <= 0) continue;

                int decodedSamples = _decoder.Decode(_receiveBuffer, 0, receivedBytes, _outPcmBuffer, 0, 960, false);

                float maxPeak = 0;
                for (int i = 0; i < decodedSamples; i++)
                {
                    float sampleVal = Math.Abs(_outPcmBuffer[i] / 32768f);
                    if (sampleVal > maxPeak) maxPeak = sampleVal;
                }
                OnRemoteVolumeChanged?.Invoke(IsDeafened ? 0 : maxPeak);

                if (!IsDeafened)
                {
                    int bytesToProvider = decodedSamples * 2;
                    Buffer.BlockCopy(_outPcmBuffer, 0, _rawBytesBuffer, 0, bytesToProvider);
                    _waveProvider.AddSamples(_rawBytesBuffer, 0, bytesToProvider);
                }
            }
            catch
            {
                break;
            }
        }
    }
}