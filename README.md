# Modbus Device Simulator

Bu uygulama, Modbus master programinizi test etmek icin sanal cihazlar olusturur.

- Varsayilan olarak `10` Modbus TCP cihaz baslatir.
- `--rtu-port COMx` verirseniz ayni anda `10` Modbus RTU slave de baslatir.
- Her cihaz `50` register sunar.
- Tum registerler `500 ms` aralikla random guncellenir.
- Sonradan `20 TCP + 20 RTU` icin sadece parametre degistirmeniz yeterlidir.

## Desteklenen fonksiyonlar

- `03 Read Holding Registers`
- `04 Read Input Registers`

## Calistirma

```powershell
cd .\ModbusDeviceSimulator
dotnet run -- --rtu-port COM10
```

20 TCP ve 20 RTU icin:

```powershell
dotnet run -- --rtu-port COM10 --tcp-count 20 --rtu-count 20
```

Sadece TCP test etmek isterseniz:

```powershell
dotnet run
```

## TCP cihaz listesi

Varsayilan ayarlarda TCP cihazlari su portlarda acilir:

- `127.0.0.1:1502`
- `127.0.0.1:1503`
- `127.0.0.1:1504`
- `127.0.0.1:1505`
- `127.0.0.1:1506`
- `127.0.0.1:1507`
- `127.0.0.1:1508`
- `127.0.0.1:1509`
- `127.0.0.1:1510`
- `127.0.0.1:1511`

Her port tek bir TCP cihazdir. Unit ID bilgisi konsolda ayrica yazdirilir.

## RTU kullanim notu

RTU icin tipik olarak bir sanal seri port cifti gerekir.

- Ornek: master uygulamaniz `COM11` uzerinden baglanir.
- Simulator `COM10` uzerinden dinler.
- `COM10 <-> COM11` birbirine bagli sanal cift olmalidir.

Varsayilan RTU slave id listesi `1..10` araligindadir. `--rtu-count 20` verirseniz `1..20` olur.

## Register davranisi

Tum cihazlarda register adresleri `0..49` araligindadir.

- Tum holding ve input register degerleri surekli random degisir.
- Guncelleme periyodu varsayilan olarak `500 ms`'dir.
- Bir register tek okumada tek bir deger dondurur; sonraki okumada farkli olabilir.

Cihazlara gore random veri bantlari farklidir:

- TCP holding registerleri `1000` tabanindan baslar.
- TCP input registerleri `15000` tabanindan baslar.
- RTU holding registerleri `30000` tabanindan baslar.
- RTU input registerleri `45000` tabanindan baslar.

Her register kendi kucuk deger bandinda rastgele degistigi icin hem hareketli veri gorursunuz hem de cihazlari ayirt etmek kolay kalir.
