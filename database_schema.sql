PGDMP                       }            mtcg    17.2    17.2      �           0    0    ENCODING    ENCODING        SET client_encoding = 'UTF8';
                           false            �           0    0 
   STDSTRINGS 
   STDSTRINGS     (   SET standard_conforming_strings = 'on';
                           false            �           0    0 
   SEARCHPATH 
   SEARCHPATH     8   SELECT pg_catalog.set_config('search_path', '', false);
                           false            �           1262    24576    mtcg    DATABASE     x   CREATE DATABASE mtcg WITH TEMPLATE = template0 ENCODING = 'UTF8' LOCALE_PROVIDER = libc LOCALE = 'German_Germany.1252';
    DROP DATABASE mtcg;
                     postgres    false                        3079    32981 	   uuid-ossp 	   EXTENSION     ?   CREATE EXTENSION IF NOT EXISTS "uuid-ossp" WITH SCHEMA public;
    DROP EXTENSION "uuid-ossp";
                        false            �           0    0    EXTENSION "uuid-ossp"    COMMENT     W   COMMENT ON EXTENSION "uuid-ossp" IS 'generate universally unique identifiers (UUIDs)';
                             false    2            �            1259    32939    cards    TABLE     �   CREATE TABLE public.cards (
    id uuid NOT NULL,
    name character varying(255) NOT NULL,
    damage double precision NOT NULL,
    package_id uuid,
    user_id integer,
    element character varying(50),
    type character varying(50)
);
    DROP TABLE public.cards;
       public         heap r       postgres    false            �            1259    32944    package_cards    TABLE     b   CREATE TABLE public.package_cards (
    package_id integer NOT NULL,
    card_id uuid NOT NULL
);
 !   DROP TABLE public.package_cards;
       public         heap r       postgres    false            �            1259    32998    packages    TABLE     \   CREATE TABLE public.packages (
    package_id uuid NOT NULL,
    number integer NOT NULL
);
    DROP TABLE public.packages;
       public         heap r       postgres    false            �            1259    33096    packages_number_seq    SEQUENCE     �   CREATE SEQUENCE public.packages_number_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;
 *   DROP SEQUENCE public.packages_number_seq;
       public               postgres    false    222            �           0    0    packages_number_seq    SEQUENCE OWNED BY     K   ALTER SEQUENCE public.packages_number_seq OWNED BY public.packages.number;
          public               postgres    false    224            �            1259    33003 	   user_deck    TABLE     [   CREATE TABLE public.user_deck (
    user_id integer NOT NULL,
    card_id uuid NOT NULL
);
    DROP TABLE public.user_deck;
       public         heap r       postgres    false            �            1259    32918    users    TABLE     �  CREATE TABLE public.users (
    username character varying(50) NOT NULL,
    password character varying(100) NOT NULL,
    image character varying(100),
    token character varying(100),
    coins integer DEFAULT 20,
    wins integer DEFAULT 0,
    losses integer DEFAULT 0,
    bio character varying(100),
    id integer NOT NULL,
    elo integer DEFAULT 100,
    ingame_name character varying(50)
);
    DROP TABLE public.users;
       public         heap r       postgres    false            �            1259    32921    users_id_seq    SEQUENCE     �   CREATE SEQUENCE public.users_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;
 #   DROP SEQUENCE public.users_id_seq;
       public               postgres    false    218            �           0    0    users_id_seq    SEQUENCE OWNED BY     =   ALTER SEQUENCE public.users_id_seq OWNED BY public.users.id;
          public               postgres    false    219            B           2604    33097    packages number    DEFAULT     r   ALTER TABLE ONLY public.packages ALTER COLUMN number SET DEFAULT nextval('public.packages_number_seq'::regclass);
 >   ALTER TABLE public.packages ALTER COLUMN number DROP DEFAULT;
       public               postgres    false    224    222            @           2604    32922    users id    DEFAULT     d   ALTER TABLE ONLY public.users ALTER COLUMN id SET DEFAULT nextval('public.users_id_seq'::regclass);
 7   ALTER TABLE public.users ALTER COLUMN id DROP DEFAULT;
       public               postgres    false    219    218            �          0    32939    cards 
   TABLE DATA           U   COPY public.cards (id, name, damage, package_id, user_id, element, type) FROM stdin;
    public               postgres    false    220   �#       �          0    32944    package_cards 
   TABLE DATA           <   COPY public.package_cards (package_id, card_id) FROM stdin;
    public               postgres    false    221   �)       �          0    32998    packages 
   TABLE DATA           6   COPY public.packages (package_id, number) FROM stdin;
    public               postgres    false    222   
*       �          0    33003 	   user_deck 
   TABLE DATA           5   COPY public.user_deck (user_id, card_id) FROM stdin;
    public               postgres    false    223    +       �          0    32918    users 
   TABLE DATA           q   COPY public.users (username, password, image, token, coins, wins, losses, bio, id, elo, ingame_name) FROM stdin;
    public               postgres    false    218   �+       �           0    0    packages_number_seq    SEQUENCE SET     B   SELECT pg_catalog.setval('public.packages_number_seq', 73, true);
          public               postgres    false    224            �           0    0    users_id_seq    SEQUENCE SET     <   SELECT pg_catalog.setval('public.users_id_seq', 307, true);
          public               postgres    false    219            F           2606    32943    cards cards_pkey 
   CONSTRAINT     N   ALTER TABLE ONLY public.cards
    ADD CONSTRAINT cards_pkey PRIMARY KEY (id);
 :   ALTER TABLE ONLY public.cards DROP CONSTRAINT cards_pkey;
       public                 postgres    false    220            H           2606    32948     package_cards package_cards_pkey 
   CONSTRAINT     o   ALTER TABLE ONLY public.package_cards
    ADD CONSTRAINT package_cards_pkey PRIMARY KEY (package_id, card_id);
 J   ALTER TABLE ONLY public.package_cards DROP CONSTRAINT package_cards_pkey;
       public                 postgres    false    221    221            J           2606    33002    packages packages_pkey 
   CONSTRAINT     \   ALTER TABLE ONLY public.packages
    ADD CONSTRAINT packages_pkey PRIMARY KEY (package_id);
 @   ALTER TABLE ONLY public.packages DROP CONSTRAINT packages_pkey;
       public                 postgres    false    222            L           2606    33007    user_deck user_deck_pkey 
   CONSTRAINT     d   ALTER TABLE ONLY public.user_deck
    ADD CONSTRAINT user_deck_pkey PRIMARY KEY (user_id, card_id);
 B   ALTER TABLE ONLY public.user_deck DROP CONSTRAINT user_deck_pkey;
       public                 postgres    false    223    223            D           2606    32927    users users_pkey 
   CONSTRAINT     N   ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);
 :   ALTER TABLE ONLY public.users DROP CONSTRAINT users_pkey;
       public                 postgres    false    218            M           2606    32954 (   package_cards package_cards_card_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.package_cards
    ADD CONSTRAINT package_cards_card_id_fkey FOREIGN KEY (card_id) REFERENCES public.cards(id) ON DELETE CASCADE;
 R   ALTER TABLE ONLY public.package_cards DROP CONSTRAINT package_cards_card_id_fkey;
       public               postgres    false    221    220    4678            N           2606    33013     user_deck user_deck_card_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.user_deck
    ADD CONSTRAINT user_deck_card_id_fkey FOREIGN KEY (card_id) REFERENCES public.cards(id) ON DELETE CASCADE;
 J   ALTER TABLE ONLY public.user_deck DROP CONSTRAINT user_deck_card_id_fkey;
       public               postgres    false    4678    223    220            O           2606    33008     user_deck user_deck_user_id_fkey    FK CONSTRAINT     �   ALTER TABLE ONLY public.user_deck
    ADD CONSTRAINT user_deck_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
 J   ALTER TABLE ONLY public.user_deck DROP CONSTRAINT user_deck_user_id_fkey;
       public               postgres    false    4676    218    223            �     x��WKs��<��R$��=�C*����^����N9����ʲG�:�hu��4�_��I�i�F��Dº�]I٣İ2�]�?�/�ܟ~�t����%4	����d8s�Xìĥ��|��ϟ���ܷm��W�����q�E���_���O��@�>�w<}+����M�S��G,�J��G���f��zz��À~9�!���Q���$mU꺄�F�^�K��_���"��X枳U�xy$i;�(A��m�E
���/�+�*�S_���%L�Z�hJ�K.��{�¼g}�����ex��+�0�l���Z}W��$��6�<
z��G!�sEO^j��(P�r�6ϡ�r�B*+��J������
�G=7N�����F�7��A<�l�9%L�P�7`ٖ��4j¤h����J��⽾�Rއ���aM�G>uSߘ�4�����B�/s*v7&M�}6�)�2r���tp��`�L��hв�R� R�Lz �]�����r�93�<�����N�+���C�T��U��Q@_�`��[�4�6��:��$ת�w��,�ǒ[ޚ0CY#�61��Ġu�L.����O��.�g�<G�v
�:� ��I�l�K�r�s���J,'1�r�8�#��M�7��u��W���ʖ>i������;��Un�W�ڢC(e��f5���{G?`r�z��.8�����w���A�y	�X[+�_�X �Iإ���	5)V���k�WX(u�e���`���*mcl��R�X�n7��fn�\I$]2�(��"��u��do�mm����Q����u�	wW�i�q�@��!�r�'gp�Ycc�y�	zn�WP� ni��q��N�pl^�AIӾy�{��1�0��k@q�@"�3h��f�[v����JW��3De�ى[��a1��J����!���^����%�����AD����h!�*'��Ə� _]���r�7M�Xd��	Ad�@�r�_�u�}��u:H��3���L�X�$R�^�XL�D]�8�#jT�	�m�����+Gx��3��=�HU�t�`0��H�k��е"�5cd-'��-�8������.?�pÃb-O��vE��=�\Hou#� o#]���"�f�:22�ٜ~P���iC�Ǭm�].�����|(��*X���B|a�N��*����1�k���x���-��� �� ��ri�i���ag�����|3�=���X��57E�H�fBB,�6��<n��p%���c�-�ɚ�R���D�%��1}���m�/�R~���@�W����~h"4T'�|(�r}i�W�΃ǋ� J(��u�g����{B�zB���5"��!�'ءq�'�v9^�x=-�k'�U^��%Wv��m;��@1�����d��;��jػ��f����{.Qk������bؖ��T8���!�WFK��Еэ�}��xt!!��jb���+=�����A(>o0���n=�}B�j;���z�v0�5�����g��aX����5'��(�x����>|�����%      �      x������ � �      �   �   x���q@1�^�	q���ㄈٷk��yH�z3A�̳��n���ů� �;@���	Cl��xg���G&�ROH����l��E�3�����b��2 �c�0|�+�����($���7�]��{wSF����~'�q�%��!�!�xw�	�'s o���?ۏ�#گ.����>�Q>-��G������e�T4o�Y鷡�|��!^	�s+o]�����3���K\      �   �   x�-�ɍ1�sW.�0`�\��1�?��J͕��8?.��.�� �� |�����#��M�3&dR�99�am����0+W��
���2�pDZ�G�B�5�F۰��։U���B�dƭD0��R�#	:���!��­e�k&޳R�*��,�?�M\��oF	p�eӣ{�P�)����5n<!;$x3ޙ�������y�}�P      �     x�m��n�@E��St�G~�g�j��AA�4q3� 00C� ���"Iͷ�7��|8.3&2�H҄'��Sr=/��Vm�ȶ�-Y�y�mͪUs�1�;�ݖ�����G��ʴ��ӎS,���Eԁ$�CCXe���Dt$�{.�,��� �ȹG������{��ٰj���B\�k�	hٗ�L�}L��В<U�2!���_��EMX�r�k�uF���C͸(M�B�8��L�dt)NR���˗������
�c�?��ǣ�6<�Q�P�q�pV     