using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Chat.viewModels
{
    public class MainViewModel : DependencyObject
    {
        private SynchronizationContext uiContext;

        private DependencyProperty listBoxItemsProperty;
        private DependencyProperty usersMessageProp;
        private DependencyProperty usersOnlineProp;

        public ObservableCollection<string> listBoxElements
        {
            get
            {
                return (ObservableCollection<string>)GetValue(listBoxItemsProperty);
            }
            set
            {
                SetValue(listBoxItemsProperty, value);
            }
        }
        public ObservableCollection<string> usersOnline
        {
            get
            {
                return (ObservableCollection<string>)GetValue(usersOnlineProp);
            }
            set
            {
                SetValue(usersOnlineProp, value);
            }
        }
        public string usersMessage
        {
            get
            {
                return (string)GetValue(usersMessageProp);
            }
            set
            {
                SetValue(usersMessageProp, value);
            }
        }

        public ICommand sendMessage
        {
            get;set;
        }

        public MainViewModel()
        {
            uiContext = SynchronizationContext.Current;

            listBoxItemsProperty = DependencyProperty.Register(
                "listBoxElements",
                typeof(ObservableCollection<string>),
                typeof(MainViewModel));


            usersOnlineProp = DependencyProperty.Register(
                "usersOnline",
                typeof(ObservableCollection<string>),
                typeof(MainViewModel));

            usersMessageProp = DependencyProperty.Register(
                "usersMessage",
                typeof(string),
                typeof(MainViewModel));

            sendMessage = new MyCommand(sendTextMessage, null);

            usersOnline = new ObservableCollection<string>();
            listBoxElements = new ObservableCollection<string>();

            WaitClientQuery();
            notifyOnline();
        }

        // прием сообщения
        private async void WaitClientQuery()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Инициализируем новый экземпляр класса UdpClient и связываем его с заданным номером локального порта.
                    UdpClient client = new UdpClient(49152 /* порт */); // принимаются все входящие соединения с локальной конечной точкой
                    client.EnableBroadcast = true;
                    while (true)
                    {
                        IPEndPoint remote = null; // информация об удаленном хосте, который отправил датаграмму
                        byte[] arr = client.Receive(ref remote); // получим UDP-датаграмму
                        MessageType type;
                        string userName;
                        if (arr.Length > 0)
                        {                    
                            // Создадим поток, резервным хранилищем которого является память.
                            MemoryStream stream = new MemoryStream(arr);
                            // BinaryFormatter сериализует и десериализует объект в двоичном формате 
                            BinaryFormatter formatter = new BinaryFormatter();

                            type = (MessageType)formatter.Deserialize(stream);
                            stream.Close();

                            switch (type)
                            {
                                case MessageType.NEW_USER_ONLINE:
                                    arr = client.Receive(ref remote);
                                    stream = new MemoryStream(arr);

                                    userName = (string)formatter.Deserialize(stream);

                                    uiContext.Send(d => usersOnline.Add(userName), null);

                                    stream.Close();
                                    break;
                                case MessageType.NEW_TEXT_MESSAGE:
                                    {
                                        arr = client.Receive(ref remote);
                                        stream = new MemoryStream(arr);

                                        Message m = (Message)formatter.Deserialize(stream);
                                                                                            
                                        uiContext.Send(d => listBoxElements.Add(m.user), null);
                                        uiContext.Send(d => listBoxElements.Add(m.mes), null);

                                        stream.Close();
                                    }
                                    break;
                                case MessageType.USER_LEFT:
                                    arr = client.Receive(ref remote);
                                    stream = new MemoryStream(arr);

                                    userName = (string)formatter.Deserialize(stream);

                                    uiContext.Send(d =>
                                    {
                                        if (usersOnline.Contains(userName))
                                        {
                                            usersOnline.Remove(userName);
                                        }

                                    }, null);

                                    stream.Close();
                                    break;

                            }

                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Получатель: " + ex.Message);
                }
            });
        }

        // отправление сообщения
        private async void sendTextMessage(object sender)
        {
            await Task.Run(() =>
            {
                try
                {
                    UdpClient client = new UdpClient( "", 49152 );
                    client.EnableBroadcast = true;
                    MemoryStream stream = new MemoryStream();
                    BinaryFormatter formatter = new BinaryFormatter();

                    Message m = new Message();
                    uiContext.Send(d => m.mes = usersMessage, null); 
                    m.user = Environment.UserName;

                    //send message type
                    MessageType type = MessageType.NEW_TEXT_MESSAGE;
                    formatter.Serialize(stream, type);
                    byte[] arr = stream.ToArray();
                    stream.Close();
                    client.Send(arr, arr.Length);

                    //send text message
                    stream = new MemoryStream();
                    formatter.Serialize(stream, m); 
                    arr = stream.ToArray();
                    stream.Close();
                    client.Send(arr, arr.Length);

                    client.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Отправитель: " + ex.Message);
                }
            });
        }
        private async void notifyOnline()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Инициализируем новый экземпляр класса UdpClient и устанавливаем удаленный узел
                    UdpClient client = new UdpClient(
                        "" /* IP-адрес удаленного DNS-узла, к которому планируется подключение. */,
                        49152 /* Номер удаленного порта, к которому планируется выполнить подключение. */ );
                    client.EnableBroadcast = true;
                    // Создадим поток, резервным хранилищем которого является память.
                    MemoryStream stream = new MemoryStream();
                    // BinaryFormatter сериализует и десериализует объект в двоичном формате 
                    BinaryFormatter formatter = new BinaryFormatter();

                    //send message type
                    MessageType type = MessageType.NEW_USER_ONLINE;

                    formatter.Serialize(stream, type);

                    byte[] arr = stream.ToArray();
                    stream.Close();
                  

                    client.Send(arr, arr.Length);

                    //send user name
                    stream = new MemoryStream();

                    formatter.Serialize(stream, Environment.UserName);

                    arr = stream.ToArray();
                    stream.Close();

                    client.Send(arr, arr.Length);
                    client.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Отправитель: " + ex.Message);
                }
            });
        }

        public async void notifyOffline()
        {
            await Task.Run(() =>
            {
                // Инициализируем новый экземпляр класса UdpClient и устанавливаем удаленный узел
                UdpClient client = new UdpClient(
                    "" /* IP-адрес удаленного DNS-узла, к которому планируется подключение. */,
                    49152 /* Номер удаленного порта, к которому планируется выполнить подключение. */ );
                client.EnableBroadcast = true;
                // Создадим поток, резервным хранилищем которого является память.
                MemoryStream stream = new MemoryStream();
                // BinaryFormatter сериализует и десериализует объект в двоичном формате 
                BinaryFormatter formatter = new BinaryFormatter();

                //send message type
                MessageType type = MessageType.USER_LEFT;

                formatter.Serialize(stream, type);

                byte[] arr = stream.ToArray();
                stream.Close();

                client.Send(arr, arr.Length);

                //send user name
                stream = new MemoryStream();

                formatter.Serialize(stream, Environment.UserName);

                arr = stream.ToArray();
                stream.Close();

                client.Send(arr, arr.Length);
                client.Close();

            });
        }
        



        [Serializable]
        struct Message
        {
            public string mes; // текст сообщения
            public string user; // имя пользователя
        }
        enum MessageType
        {
            NEW_USER_ONLINE = 1,
            NEW_TEXT_MESSAGE = 2,
            USER_LEFT = 3
        }
    }
}
