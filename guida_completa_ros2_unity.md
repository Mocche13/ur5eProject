# Guida Completa: Connessione tra ROS 2 (WSL) e Unity

Questa guida racchiude tutte le informazioni, la teoria e i passaggi pratici per configurare, avviare e diagnosticare la connessione tra **Unity** (in esecuzione su Windows) e **ROS 2** (in esecuzione su WSL - Windows Subsystem for Linux).

Prima di iniziare a lavorare devi setuppare ROS2 sul tuo PC. La guida è al seguente link di yt: https://www.youtube.com/watch?v=Gg25GfA456o&t=657s. Devi seguire i primi 20 minuti circa, attento che il tutorial è fatto su ROS2/humble (edizione del 2022) ma nel 2026 devi sostituire le parti del codice di installazione con scritto humble con la scritta 'lyrical'. Tranquill*, funziona tutto, ROS non è come Unity e le versioni più nuove si adattano senza problemi a quelle vecchie.

---

## 1. Prerequisiti e Informazioni Generali

### Come funziona la connessione
Unity e ROS 2 non comunicano nativamente tramite DDS a causa delle limitazioni di rete virtuale di WSL2. Per farli dialogare si utilizza un sistema di comunicazione basato su TCP tramite due componenti:
1. Il pacchetto **ROS-TCP-Connector** installato dentro Unity.
2. Il nodo ROS 2 **ROS-TCP-Endpoint** in esecuzione dentro WSL.

### Indirizzi IP (`127.0.0.1` vs `0.0.0.0` vs IP WSL)
* **In ROS 2 (lato server):** Si usa sempre `ROS_IP:=0.0.0.0`. Questo significa "ascolta su tutte le interfacce di rete disponibili", permettendo a Windows/Unity di comunicare con WSL.
* **In Unity (lato client):** Si imposta di norma **`127.X.X`** (grazie al localhost forwarding di WSL2). Per far funzionare la connessione tale IP va sostituito con l'IP effettivo di WSL caratteristico di ogni PC.

---

## 2. Come trovare l'IP di WSL
Puoi trovare l'IP esatto della tua istanza WSL aprendo il terminale di Ubuntu e digitando:
```bash
hostname -I
```
Oppure, in modo più dettagliato (dovrebbe bastare il primo):
```bash
ip addr show eth0
```

---

## 3. Checklist per ogni sessione di lavoro (Workflow Giornaliero)

Segui questi passaggi ogni volta che accendi il PC e vuoi avviare una sessione di sviluppo:

### Passo 1: Avviare l'Endpoint in ROS 2 (WSL)
1. Apri il terminale di Ubuntu su WSL.
2. Spostati nel tuo workspace (es. `ros2_ws`) e carica l'ambiente:
   ```bash
   cd ~/ros2_ws
   source install/setup.bash
   ```
3. Avvia il server TCP di ROS 2:
   ```bash
   ros2 run ros_tcp_endpoint default_server_endpoint --ros-args -p ROS_IP:=0.0.0.0
   ```
   * *Verifica:* Il terminale deve mostrare: `[INFO] [UnityEndpoint]: Starting server on 0.0.0.0:10000`
   * *Nota*: Il terminale riporterà un messaggio di errore ma tale errore rappresenta solo che potenzialmente ROS potrebbe ricevere 3 segnali ma ne sta cercando solo 1, per il nostro progetto tale errore non rappresenta alcun vincolo e si può ignorare.

### Passo 2: Configurare e Avviare Unity (Windows)
1. Apri il tuo progetto in Unity.
2. Vai su **Robotics** > **ROS Settings**:
   * **Protocol:** `ROS2`
   * **ROS IP Address:** `127.X.X.X` (o l'IP di WSL se riscontri timeout)
   * **ROS Port:** `10000`
3. Premi il tasto **Play** nell'editor di Unity per avviare la simulazione e stabilire il collegamento.

---

## 4. Installazione Iniziale del Pacchetto (Una tantum)
Se stai configurando il progetto su un nuovo workspace o WSL da zero, i passaggi di installazione sono i seguenti:

1. Crea e posizionati nel workspace:
   ```bash
   mkdir -p ~/ros2_ws/src
   cd ~/ros2_ws/src
   ```
2. Clona il pacchetto ufficiale ROS-TCP-Endpoint per ROS 2:
   ```bash
   git clone -b main-ros2 https://github.com/Unity-Technologies/ROS-TCP-Endpoint.git
   ```
3. Torna alla radice del workspace e compila:
   ```bash
   cd ..
   colcon build
   ```
   *(Nota: eventuali avvisi di tipo `SetuptoolsDeprecationWarning` durante la compilazione sono normali e possono essere ignorati).*

---

## 5. Risoluzione dei Problemi Comuni

* **Errore di Timeout / Mancata Connessione:**
  Assicurati che il terminale WSL con il comando `ros2 run` sia attivo *prima* di premere Play in Unity. Se persiste, gemini non ha soluzione per questo (aveva scritto una cazzata in questo markdown) e quindi arrangiati.