using Microsoft.AspNetCore.SignalR;
using UnoOnline.Model;

namespace UnoOnline.SignalR2.Hubs
{
    public class UnoOnlineHub : Hub
    {
        private static StatusPartida? statusPartida;

        public UnoOnlineHub()
        {

        }

        public override Task OnConnectedAsync()
        {
            if (statusPartida == null)
            {
                statusPartida = new StatusPartida();
                statusPartida.GeraBaralho();
            }
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Jogador jogador = statusPartida.Jogadores.Where(w => w.ConnectionId == Context.ConnectionId).FirstOrDefault();
            if (jogador != null)
            {
                statusPartida.Jogadores.Remove(jogador);
                Console.WriteLine($"Jogador {jogador.Nome} saiu da partida.");
            }
            return base.OnDisconnectedAsync(exception);
        }
        public async Task EnviaUltimaCartaJogada(Carta carta, Jogador jogador)
        {
            //Recupero a carta atualmente jogada por último
            var ultCartaJogada = statusPartida.Baralho[statusPartida.UltimaCarta.Uuid];
            ultCartaJogada.EstaEmBaralho = true;

            //Ultima carta no status recebe a nova carta que foi jogada
            statusPartida.UltimaCarta = carta;

            var jg = statusPartida.Jogadores.Where(w => w.Uuid == jogador.Uuid).First();
            var novaCarta = statusPartida.Baralho[carta.Uuid];

            statusPartida.Jogadores.Find(jg).Value.Cartas.Remove(novaCarta);
            var currentNode = statusPartida.Jogadores.Find(jg);

            switch (novaCarta.Tipo)
            {
                case "bloqueio":
                    statusPartida.PassarVez(2);
                    break;
                case "inverter":
                    statusPartida.MudarSentido();
                    statusPartida.PassarVez(1);
                    break;
                case "coringa-maisdois":
                    statusPartida.ComprarCartas(statusPartida.RetornaProximoJogador(jg), 2);
                    statusPartida.PassarVez(2);
                    break;
                case "coringa-maisquatro":
                    statusPartida.ComprarCartas(statusPartida.RetornaProximoJogador(jg), 4);
                    statusPartida.PassarVez(2);
                    break;
                default:
                    statusPartida.PassarVez(1);
                    break;
            }

            await EnviaStatusPartida(statusPartida);
        }
        public async Task ComprarCarta(Jogador jogador)
        {
            var jg = statusPartida.Jogadores.Where(w => w.Uuid == jogador.Uuid).First();
            statusPartida.ComprarCartas(jg, 1);
            await EnviaStatusPartida(statusPartida);
        }
        public async Task EnviaStatusPartida(StatusPartida statusPartida)
        {
            await Clients.All.SendAsync("AtualizarStatusPartida", statusPartida);
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
            await EnviaStatusPartida(statusPartida);
        }
    }
}
