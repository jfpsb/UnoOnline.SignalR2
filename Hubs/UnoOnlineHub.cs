using Microsoft.AspNetCore.SignalR;
using System.Timers;
using UnoOnline.Model;

namespace UnoOnline.SignalR2.Hubs
{
    public class UnoOnlineHub : Hub
    {
        private static StatusPartida statusPartida; //Guarda os dados da partida
        private System.Timers.Timer timerJogada; //Timer para jogada
        private static DateTime expiraEm; //Guarda quando a vez do jogador atual expira
        private string _segundosAteExpiracaoEmString;
        private int segundosTimer = 15; //Segundos máximo para cada jogador fazer sua jogada
        internal static IHubCallerClients MyClients { get; set; }

        /// <summary>
        /// Ao receber nova conexão de cliente, caso seja o primeiro jogador instancio o status da partida e gero o baralho.
        /// </summary>
        public override Task OnConnectedAsync()
        {
            if (statusPartida == null)
            {
                statusPartida = new StatusPartida();
            }
            MyClients ??= Clients;
            return base.OnConnectedAsync();
        }
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Jogador jogador = statusPartida.Jogadores.Where(w => w.ConnectionId == Context.ConnectionId).FirstOrDefault();
            if (jogador != null)
            {
                //Jogador da vez saiu da partida, passo a vez para proximo jogador
                if (statusPartida.JogadorDaVez.Uuid == jogador.Uuid)
                {
                    statusPartida.PassarVez(1, jogador);
                }
                statusPartida.Jogadores.Remove(jogador);
                EnviaEventosPartida($"Jogador {jogador.Nome} saiu da partida.");
                EnviaStatusPartida(statusPartida);
            }
            return base.OnDisconnectedAsync(exception);
        }
        public async Task EnviaUltimaCartaJogada(Carta carta, Jogador jogador)
        {
            //Testo se quem fez a jogada foi o jogador da vez ou se houve corte
            if (jogador.Uuid != statusPartida.JogadorDaVez.Uuid)
            {
                await EnviaEventosPartida($"{jogador.Nome} realizou um corte e {statusPartida.JogadorDaVez.Nome} perdeu a vez.");
            }

            //Recupero a carta atualmente jogada por último
            var ultCartaJogada = statusPartida.Baralho[statusPartida.UltimaCarta.Uuid];
            ultCartaJogada.EstaEmBaralho = true;

            //Ultima carta no status recebe a nova carta que foi jogada
            statusPartida.UltimaCarta = carta;

            var jg = statusPartida.Jogadores.Where(w => w.Uuid == jogador.Uuid).First(); //Retorno instancia de jogador no servidor
            var novaCarta = statusPartida.Baralho[carta.Uuid]; //Retorno instancia de carta no servidor
            novaCarta.Cor = carta.Cor;

            //Testa se jogador gritou Uno
            if (jogador.Cartas.Count == 2 && !jogador.GritouUno)
            {
                //Não gritou Uno. Toma punição e compra duas cartas.
                statusPartida.ComprarCartas(jg, 2);
                await EnviaEventosPartida($"Jogador {jogador.Nome} não gritou Uno e foi punido com mais duas cartas.");
            }

            var currentNode = statusPartida.Jogadores.Find(jg);
            currentNode.Value.Cartas.Remove(novaCarta);

            //Se após jogar a carta tiver zero cartas este jogador é o vencedor
            if (currentNode.Value.Cartas.Count == 0)
            {
                await EnviaEventosPartida($"Jogador {jogador.Nome} venceu a partida.");
                await Clients.All.SendAsync("AcabouPartida", jogador);
                statusPartida = new StatusPartida();
                DestroiTimer();
                return;
            }

            var proxJg = statusPartida.RetornaProximoJogador(jg); //Retorno próximo jogador da sequência

            switch (novaCarta.Tipo)
            {
                case "bloqueio":
                    statusPartida.PassarVez(2, jg);
                    await EnviaEventosPartida($"{jogador.Nome} bloqueou a vez de {proxJg.Nome}.");
                    break;
                case "inverter":
                    statusPartida.MudarSentido();
                    statusPartida.PassarVez(1, jg);
                    await EnviaEventosPartida($"{jogador.Nome} inverteu a ordem do jogo.");
                    break;
                case "coringa-maisdois":
                    statusPartida.ComprarCartas(proxJg, 2);
                    statusPartida.PassarVez(2, jg);
                    await EnviaEventosPartida($"{jogador.Nome} comprou duas cartas para {proxJg.Nome}.");
                    break;
                case "coringa-maisquatro":
                    statusPartida.ComprarCartas(statusPartida.RetornaProximoJogador(jg), 4);
                    statusPartida.PassarVez(2, jg);
                    await EnviaEventosPartida($"{jogador.Nome} comprou quatro cartas para {proxJg.Nome} e escolheu a cor {novaCarta.Cor.ToUpper()}.");
                    break;
                case "coringa-cores":
                    await EnviaEventosPartida($"{jogador.Nome} escolheu a cor {novaCarta.Cor.ToUpper()}.");
                    statusPartida.PassarVez(1, jg);
                    break;
                default:
                    statusPartida.PassarVez(1, jg);
                    break;
            }

            expiraEm = DateTime.Now.AddSeconds(segundosTimer);
            await EnviaStatusPartida(statusPartida);
        }
        public async Task ComprarCarta(Jogador jogador)
        {
            expiraEm = DateTime.Now.AddSeconds(segundosTimer);
            var jg = statusPartida.Jogadores.Where(w => w.Uuid == jogador.Uuid).First();
            statusPartida.ComprarCartas(jg, 1);
            await EnviaStatusPartida(statusPartida);
        }
        public async Task EnviaEventosPartida(string evento)
        {
            await MyClients.All.SendAsync("RecebeEventoJogo", evento);
        }
        public async Task EnviaStatusPartida(StatusPartida statusPartida)
        {
            await MyClients.All.SendAsync("AtualizarStatusPartida", statusPartida);
        }
        public async Task EntrarEmPartida(Jogador jogador)
        {
            if (statusPartida.Jogadores.Count == 0)
                statusPartida.UltimaCarta = statusPartida.RetornaCartasDoBaralho(1, true).FirstOrDefault();

            if (statusPartida.Jogadores.Count >= 4)
            {
                await Clients.Caller.SendAsync("MensagemMessageBox", "A sala do jogo está cheia!");
                return;
            }

            foreach (var item in statusPartida.RetornaCartasDoBaralho(7, false))
            {
                jogador.Cartas.Add(item);
            }

            jogador.ConnectionId = Context.ConnectionId;
            statusPartida.AdicionaJogador(jogador);

            if (statusPartida.Jogadores.Count == 2)
                IniciaTimer();

            await EnviaEventosPartida($"Jogador {jogador.Nome} entrou na partida!");
            await EnviaStatusPartida(statusPartida);
        }
        private void IniciaTimer()
        {
            expiraEm = DateTime.Now.AddSeconds(segundosTimer);
            timerJogada = new System.Timers.Timer(1);
            timerJogada.AutoReset = true;
            timerJogada.Enabled = true;
            timerJogada.Elapsed += TimerJogada_Elapsed;
        }
        private void DestroiTimer()
        {
            if (timerJogada != null)
            {
                timerJogada.Stop();
                timerJogada.Dispose();
            }
        }
        private async void TimerJogada_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (e.SignalTime > expiraEm)
            {
                statusPartida.PassarVez(1, statusPartida.JogadorDaVez);
                expiraEm = DateTime.Now.AddSeconds(segundosTimer);
                await EnviaEventosPartida($"{statusPartida.JogadorDaVez.Nome} não jogou a tempo e perdeu a vez.");
                await EnviaStatusPartida(statusPartida);
            }
            else
            {
                var horaAtual = DateTime.Now.TimeOfDay;
                var timeSpan2 = new TimeSpan(expiraEm.Hour, expiraEm.Minute, expiraEm.Second);

                if (horaAtual <= timeSpan2)
                {
                    var timeSpanRestante = timeSpan2.Subtract(horaAtual);
                    SegundosAteExpiracaoEmString = timeSpanRestante.ToString(@"ss\.f");
                }
            }

            await MyClients.All.SendAsync("AtualizarTimer", SegundosAteExpiracaoEmString);
        }
        public string SegundosAteExpiracaoEmString
        {
            get
            {
                return _segundosAteExpiracaoEmString;
            }

            set
            {
                _segundosAteExpiracaoEmString = value;
            }
        }
    }
}
